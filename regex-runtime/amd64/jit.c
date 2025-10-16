#include "../jit.h"
#include "../api.h"
#include "../common.h"
#include "../mmap.h"
#include "../vec.h"
#include <assert.h>
#include <errno.h>
#include <setjmp.h>
#include <stddef.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "asm.h"

// JIT code is a function that transits given NFA states.
// Each step consumes one char from the given buffer.
// Function returns if it reached the end of the buffer, or if sinked.
// It uses non-standard calling convention, so it's wrapped into a C function.
//
// 0-th byte (lowest) of the return is "reached accepting state at the last step" flag.
// 1-th byte of the return is "non-sinked" flag (true iff there are active states in the bitmap)
//
// Synopsis:
//     uint64_t jit_code(
//         const uint8_t *buf,
//         size_t buf_len,
//         uint64_t states_bitmap0,
//         uint64_t states_bitmap1,
//         uint64_t states_bitmap2,
//         uint64_t states_bitmap3
//     )
//
// Registers used:
//    rsi (input)          - `buf` (incremented on each step)
//    rdi (input)          - `buf_end`
//    r8  (input & output) - `states_bitmap0`
//    r9  (input & output) - `states_bitmap1`
//    r10 (input & output) - `states_bitmap2`
//    r11 (input & output) - `states_bitmap3`
//
//    rax (output) - return
//
//    r12-15 (internal use) - next bitmap
//    rdx    (internal use) - current byte
//    rflags (internal use)

static void emit_range_code(
    struct asm *as,
    const struct rcs_nfa_char_range *range,
    asm_label next_range,
    asm_label exit
) {
    asm_cmp_cur_char(as, range->start);
    if (range->start == range->end) {
        asm_jz(as, exit);
        return;
    }
    asm_jl(as, next_range);
    asm_cmp_cur_char(as, range->end);
    asm_jle(as, exit);
}

static void emit_next_states_bitmask_update(
    struct asm *as,
    const struct rcs_nfa *nfa,
    const struct rcs_nfa_state *state
) {
    for (size_t i = 0; i < state->next_len; ++i) {
        size_t next_i = state->next[i] - nfa->states;
        asm_bts_r64(as, ASM_R12 + (next_i / 64), next_i % 64); // bts r12-15, next_i
    }
    if (state->next_len != 0)
        asm_set_no_sink_flag(as); // mov ah, 1
}

static void emit_state_code(struct asm *as, const struct rcs_nfa *nfa, size_t state_idx) {
    const struct rcs_nfa_state *state = &nfa->states[state_idx];
    assert(!rcs_nfa_state_is_accept(state));

    // Label exit from state's match code.
    // Means success on regular match, failure on reversed.
    const asm_label end = asm_new_label(as);
    // Label next state code.
    const asm_label next_state = asm_new_label(as);

    for (size_t i = 0; i < state->ranges_len; ++i) {
        const asm_label match_continue = asm_new_label(as);
        emit_range_code(as, &state->ranges[i], match_continue, end);
        asm_place_label(as, match_continue);
    }

    if (state->inverted_match) {
        emit_next_states_bitmask_update(as, nfa, state);
        asm_place_label(as, end);
    } else {
        asm_jmp(as, next_state);                         //     jmp next_state
        asm_place_label(as, end);                        // end:
        emit_next_states_bitmask_update(as, nfa, state); //     ...
    }

    asm_place_label(as, next_state); // next_state:
}

static void emit_code(struct asm *as, const struct rcs_nfa *nfa) {
    assert(nfa->states_len <= 256);

    size_t bitmap_regs = RCS_DIV_CEILING(nfa->states_len, 64);

    asm_label loop = asm_new_label(as);
    asm_label end = asm_new_label(as);

    asm_xor_r64(as, ASM_AX, ASM_AX);               //     xor    rax, rax
    asm_calc_arr_end(as);                          //     lea    rdi, [rsi+rdi]
    asm_set_no_sink_flag(as);                      //     mov    ah, 1
    asm_place_label(as, loop);                     // loop:
    asm_test_no_sink_flag(as);                     //     test   ah, ah
    asm_jz(as, end);                               //     jz     end
    for (size_t i = 0; i < bitmap_regs; ++i)       //
        asm_xor_r64(as, ASM_R12 + i, ASM_R12 + i); //     xor    r12..15, r12..15
    asm_cmp_r64(as, ASM_SI, ASM_DI);               //     cmp    rsi, rdi
    asm_jz(as, end);                               //     je     end
    asm_xor_r64(as, ASM_AX, ASM_AX);               //     xor    rax, rax
    asm_load_char(as);                             //     movzx  edx, byte [rsi]
    asm_inc_r64(as, ASM_SI);                       //     inc    rsi

    for (size_t i = 0; i < nfa->states_len; ++i) {
        asm_shr_r64(as, ASM_R8 + i / 64); //     shr r8-12, 1

        if (rcs_nfa_state_is_accept(&nfa->states[i]))
            continue;

        asm_label skip_state = asm_new_label(as);

        asm_jnc(as, skip_state);         //     jnc skip_state
        emit_state_code(as, nfa, i);     //     ...
        asm_place_label(as, skip_state); // skip_state:
    }

    size_t accepting_state_i = nfa->accept - nfa->states;
    asm_btr_r64(
        as,
        ASM_R12 + accepting_state_i / 64,
        accepting_state_i % 64
    );                                            //     btr    r12-15, accepting_state_bit
    asm_setc_r8(as, ASM_AX);                      //     setc   al
    for (size_t i = 0; i < bitmap_regs; ++i)      //
        asm_mov_r64(as, ASM_R8 + i, ASM_R12 + i); //     mov    r8..11, r12..15
    asm_jmp(as, loop);                            //     jmp    loop
    asm_place_label(as, end);                     // end:
    asm_ret(as);                                  //     ret

    // // for manual testing
    // asm_label L1 = asm_new_label(as);
    // asm_place_label(as, L1);
    // asm_label L11 = asm_new_label(as);
    // asm_place_label(as, L11);
    // asm_jump(as, ASM_NO_CONDITION, L1);

    // asm_label L2 = asm_new_label(as);
    // asm_jump(as, ASM_NO_CONDITION, L2);
    // asm_nop(as);
    // asm_place_label(as, L2);

    // asm_label L3 = asm_new_label(as);
    // asm_place_label(as, L3);
    // asm_label L31 = asm_new_label(as);
    // asm_place_label(as, L31);
    // asm_nop(as);
    // asm_jump(as, ASM_NO_CONDITION, L3);

    // asm_label L4 = asm_new_label(as);
    // asm_jump(as, ASM_NO_CONDITION, L4);
    // asm_place_label(as, L4);
    // asm_label L41 = asm_new_label(as);
    // asm_place_label(as, L41);

    // asm_label L5 = asm_new_label(as);
    // asm_jump(as, ASM_NO_CONDITION, L5);
    // for (int i = 0; i < 100; ++i)
    //     asm_nop(as);
    // asm_label L51 = asm_new_label(as);
    // asm_label L52 = asm_new_label(as);
    // asm_place_label(as, L52);
    // asm_place_label(as, L51);
    // asm_place_label(as, L5);
    // for (int i = 0; i < 100; ++i)
    //     asm_nop(as);
    // asm_jump(as, ASM_NO_CONDITION, L5);

    // asm_label L6 = asm_new_label(as);
    // asm_label L7 = asm_new_label(as);
    // asm_place_label(as, L6);
    // asm_jump(as, ASM_NO_CONDITION, L7);
    // for (int i = 0; i < 128; ++i)
    //     asm_nop(as);
    // asm_place_label(as, L7);
    // asm_jump(as, ASM_NO_CONDITION, L6);
}

static RCS_NODISCARD rcs_error
init_jit(struct rcs_jit_scanner *scanner, const struct rcs_nfa *nfa) {
    rcs_error err = RCS_OK;

    struct asm *volatile as = malloc(sizeof *as);
    if (as == NULL)
        return RCS_MAKE_ERR_LIBC(errno);

    err = asm_init(as);
    if (rcs_failed(err))
        goto exit_free;

    // exception handler for assembler
    // reduces boilerplate
    if (setjmp(as->env) == 0) {
        memset(scanner->initial_states_bitmap, 0, sizeof scanner->initial_states_bitmap);
        scanner->has_accepting_source = false;

        for (size_t i = 0; i < nfa->sources_len; ++i) {
            size_t src_i = nfa->sources[i] - nfa->states;
            scanner->initial_states_bitmap[src_i / 64] |= 1 << (src_i % 64);

            if (rcs_nfa_state_is_accept(nfa->sources[i]))
                scanner->has_accepting_source = true;
        }

        emit_code(as, nfa);

        // {
        //     FILE *f = fopen("/tmp/regex-cs-jit2.bin", "w+");
        //     if (f == NULL)
        //         return RCS_MAKE_ERR_LIBC(errno);
        //     fwrite(as.code.data, sizeof(uint8_t), as.code.len, f);
        //     fclose(f);
        // }

        size_t bytes_optimized = asm_optimize_jumps(as);
        scanner->mmap_len = as->code.len - bytes_optimized;

        rcs_error err = rcs_mmap_for_write(&scanner->mmap_addr, scanner->mmap_len);
        if (rcs_failed(err))
            return err;

        asm_link(as, scanner->mmap_addr, scanner->mmap_len);

        //
        // {
        //     FILE *f = fopen("/tmp/regex-cs-jit.bin", "w+");
        //     if (f == NULL)
        //         return RCS_MAKE_ERR_LIBC(errno);
        //     fwrite(scanner->exec_mmap_addr, sizeof(uint8_t), scanner->exec_mmap_len, f);
        //     fprintf(stderr, "%p\n", scanner->exec_mmap_addr);
        //     fclose(f);
        // }

        err = rcs_mmap_make_exec(scanner->mmap_addr, scanner->mmap_len);
        if (rcs_failed(err))
            goto exit_free;
    } else {
        err = as->err;
    }

exit_free:
    free(as);
    return err;
}

RCS_NODISCARD
bool rcs_jit_scanner_init(
    rcs_error *err,
    struct rcs_jit_scanner *scanner,
    const struct rcs_nfa *nfa
) {
    if (nfa->states_len > 256)
        return false;

    *err = init_jit(scanner, nfa);
    return true;
}

RCS_NODISCARD
rcs_error rcs_jit_match(
    rcs_api_bool *out_ok,
    struct rcs_jit_scanner *scanner,
    const struct rcs_reader *reader
) {
    rcs_api_size n;
    uint64_t jit_return = 0x0100 | (scanner->has_accepting_source ? 1 : 0);
    void *scanner_entrypoint = scanner->mmap_addr;

    uint64_t bitmap[4] = {0};
    memcpy(bitmap, scanner->initial_states_bitmap, sizeof bitmap);

    *out_ok = false;
    while ((n = reader->read(reader->arg)) > 0) {
        void *reader_buf = reader->buf;

        __asm__ volatile(
            "    movq %6, %%rsi \n"
            "    movq %7, %%rdi \n"
            "    movq %1, %%r8  \n"
            "    movq %2, %%r9  \n"
            "    movq %3, %%r10 \n"
            "    movq %4, %%r11 \n"
            "    call *%5       \n"
            "    movq %%r8, %1  \n"
            "    movq %%r9, %2  \n"
            "    movq %%r10, %3 \n"
            "    movq %%r11, %4 \n"
            : "=a"(jit_return), "+g"(bitmap[0]), "+g"(bitmap[1]), "+g"(bitmap[2]), "+g"(bitmap[3])
            : "g"(scanner_entrypoint), "g"(reader_buf), "g"((uint64_t)n)
            : "rdx", "rsi", "rdi", "r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15"
        );
        // RCS_BREAKPOINT();
        if (!(jit_return & 0xff00))
            // sink; no match
            return RCS_OK;
    }
    *out_ok = (jit_return & 0xff) != 0;
    return RCS_OK;
}

// Does not free the scanner struct itself, only its inner buffers.
void rcs_jit_scanner_free(struct rcs_jit_scanner *scanner) {
    rcs_mmap_free(scanner->mmap_addr, scanner->mmap_len);
}
