// included from jit_amd64.c

#include "../api.h"
#include "../vec.h"
#include <setjmp.h>
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>
#include <string.h>

typedef uint32_t asm_label;
static const asm_label ASM_NO_LABEL = UINT32_MAX;
static const size_t ASM_LABEL_NO_ADDRESS = SIZE_MAX;

static const size_t ASM_NO_CONDITION = UINT8_MAX;

struct asm_jump_rec {
    asm_label to_label;
    size_t instr_addr_pass1; // address of instruction in generated pass I code.
    size_t instr_addr_pass2; // address of instruction in generated pass II code.
    // For conditional: low nibble of conditional opcode byte.
    // `NO_CONDITION` for non-conditional jump.
    uint8_t condition;
    bool is_rel32; // true after pass I, since it's pessimistic
};

struct asm {
    jmp_buf env;
    rcs_error err; // set on fail longjmp

    asm_label next_label;

    // uint8_t array with placeholders insted of jump instructions.
    struct rcs_vec code;
    struct rcs_vec label_addrs;       // size_t (label address) array indexed by label
    struct rcs_vec label_idx_ordered; // label array of ordered label indices
    struct rcs_vec jumps;             // asm_jump_rec (jump instr addresses) array
};

static size_t asm_next_address(struct asm *as) {
    return as->code.len;
}

static void asm_fail(struct asm *as, rcs_error err) {
    as->err = err;
    longjmp(as->env, 1);
}

static rcs_error asm_init(struct asm *as) {
    rcs_error err;

    as->next_label = 0;

    as->code = rcs_zero_vec;
    as->label_addrs = rcs_zero_vec;
    as->label_idx_ordered = rcs_zero_vec;
    as->jumps = rcs_zero_vec;

    err = rcs_vec_init(&as->code, sizeof(uint8_t), 4096);
    if (rcs_failed(err))
        goto err_free;
    err = rcs_vec_init(&as->label_addrs, sizeof(size_t), 8);
    if (rcs_failed(err))
        goto err_free;
    err = rcs_vec_init(&as->label_idx_ordered, sizeof(asm_label), 8);
    if (rcs_failed(err))
        goto err_free;
    err = rcs_vec_init(&as->jumps, sizeof(struct asm_jump_rec), 16);
    if (rcs_failed(err))
        goto err_free;

    return RCS_OK;

err_free:
    rcs_vec_free_data(&as->code);
    rcs_vec_free_data(&as->label_addrs);
    rcs_vec_free_data(&as->label_idx_ordered);
    rcs_vec_free_data(&as->jumps);
    return err;
}

// Label address must be set later.
RCS_NODISCARD
static asm_label asm_new_label(struct asm *as) {
    size_t addr = ASM_LABEL_NO_ADDRESS;
    rcs_error err = rcs_vec_push(&as->label_addrs, &addr);
    if (rcs_failed(err))
        asm_fail(as, err);

    return as->label_addrs.len - 1;
}

static void asm_place_label(struct asm *as, asm_label label) {
    assert(label != ASM_NO_LABEL && "unset label");

    *rcs_vec_element(&as->label_addrs, label, size_t) = asm_next_address(as);

    rcs_error err = rcs_vec_push(&as->label_idx_ordered, &label);
    if (rcs_failed(err))
        asm_fail(as, err);
}

static void asm_bytes(struct asm *as, uint8_t *b, size_t len) {
    rcs_error err = rcs_vec_push_many(&as->code, b, len);
    if (rcs_failed(err))
        asm_fail(as, err);
}

enum asm_register {
    ASM_AX = 0,
    ASM_CX,
    ASM_DX,
    ASM_BX,
    ASM_SP,
    ASM_BP,
    ASM_SI,
    ASM_DI,
    ASM_R8,
    ASM_R9,
    ASM_R10,
    ASM_R11,
    ASM_R12,
    ASM_R13,
    ASM_R14,
    ASM_R15
};

static void
asm_general_binop_r(struct asm *as, uint8_t opcode, enum asm_register dst, enum asm_register src) {
    uint8_t rex = 0x48;
    if (dst >= ASM_R8) {
        rex |= 0x1;
        dst -= ASM_R8;
    }
    if (src >= ASM_R8) {
        rex |= 0x4;
        src -= ASM_R8;
    }
    uint8_t modrm = 0xc0 | src << 3 | dst;
    uint8_t bytes[] = {rex, opcode, modrm};
    asm_bytes(as, bytes, RCS_ARRAY_LEN(bytes));
}

// cmp dl, imm8
static void asm_cmp_cur_char(struct asm *as, uint8_t imm) {
    uint8_t bytes[] = {0x80, 0xfa, imm};
    asm_bytes(as, bytes, RCS_ARRAY_LEN(bytes));
}

static void asm_ret(struct asm *as) {
    uint8_t bytes[] = {0xc3};
    asm_bytes(as, bytes, RCS_ARRAY_LEN(bytes));
}

// static void asm_int3(struct asm *as) {
//     uint8_t bytes[] = {0xcc};
//     asm_bytes(as, bytes, RCS_ARRAY_LEN(bytes));
// }

static void asm_nop(struct asm *as) {
    uint8_t bytes[] = {0x90};
    asm_bytes(as, bytes, RCS_ARRAY_LEN(bytes));
}

// Call with `cond = NO_CONDITION` if jump is unconditional.
static void asm_jump(struct asm *as, uint8_t cond, asm_label to) {
    struct asm_jump_rec jump = {
        .to_label = to,
        .instr_addr_pass1 = asm_next_address(as),
        .instr_addr_pass2 = asm_next_address(as),
        .condition = cond,
        .is_rel32 = true,
    };
    rcs_error err = rcs_vec_push(&as->jumps, &jump);
    if (rcs_failed(err))
        asm_fail(as, err);

    size_t instr_len = cond == ASM_NO_CONDITION ? 5 : 6;
    for (size_t i = 0; i < instr_len; ++i)
        asm_nop(as);
}

static void asm_jmp(struct asm *as, asm_label to) {
    asm_jump(as, ASM_NO_CONDITION, to);
}

static void asm_jz(struct asm *as, asm_label to) {
    asm_jump(as, 0x4, to);
}

static void asm_jl(struct asm *as, asm_label to) {
    asm_jump(as, 0xc, to);
}

static void asm_jle(struct asm *as, asm_label to) {
    asm_jump(as, 0xe, to);
}

static void asm_jnc(struct asm *as, asm_label to) {
    asm_jump(as, 0x3, to);
}

// movzx edx, byte [rsi]
static void asm_load_char(struct asm *as) {
    uint8_t bytes[] = {0x0f, 0xb6, ASM_DX << 3 | ASM_SI};
    asm_bytes(as, bytes, RCS_ARRAY_LEN(bytes));
}

// xor r64, r64
static void asm_xor_r64(struct asm *as, enum asm_register r1, enum asm_register r2) {
    asm_general_binop_r(as, 0x31, r1, r2);
}

// cmp r64, r64
static void asm_cmp_r64(struct asm *as, enum asm_register r1, enum asm_register r2) {
    asm_general_binop_r(as, 0x39, r1, r2);
}

// mov r64, r64
static void asm_mov_r64(struct asm *as, enum asm_register r1, enum asm_register r2) {
    asm_general_binop_r(as, 0x89, r1, r2);
}

// inc r64
static void asm_inc_r64(struct asm *as, enum asm_register r) {
    uint8_t bytes[] = {0x48, 0xff, 0xc0 | r};
    asm_bytes(as, bytes, RCS_ARRAY_LEN(bytes));
}

// shr r64, 1
static void asm_shr_r64(struct asm *as, enum asm_register r) {
    uint8_t bytes[] = {0x49, 0xd1, 0xe0 | r};
    asm_bytes(as, bytes, RCS_ARRAY_LEN(bytes));
}

enum asm_bts_mode { ASM_BT = 0, ASM_BTS, ASM_BTR };

// bts (if `do_set`) or btr
static void asm_btx_r64(struct asm *as, enum asm_bts_mode mode, enum asm_register r, uint8_t bit) {
    uint8_t rex = 0x48;
    if (r >= ASM_R8) {
        rex |= 0x1;
        r -= ASM_R8;
    }
    uint8_t bytes[] = {rex, 0x0f, 0xba, 0xe0 | mode << 3 | r, bit};
    asm_bytes(as, bytes, RCS_ARRAY_LEN(bytes));
}

// bts r64, imm8
static void asm_bts_r64(struct asm *as, enum asm_register r, uint8_t bit) {
    asm_btx_r64(as, ASM_BTS, r, bit);
}

// btr r64, imm8
static void asm_btr_r64(struct asm *as, enum asm_register r, uint8_t bit) {
    asm_btx_r64(as, ASM_BTR, r, bit);
}

static void asm_setc_r8(struct asm *as, enum asm_register r) {
    uint8_t bytes[] = {0x0f, 0x92, 0xc0 | r};
    asm_bytes(as, bytes, RCS_ARRAY_LEN(bytes));
}

// lea rdi, [rsi+rdi]
static void asm_calc_arr_end(struct asm *as) {
    uint8_t bytes[] = {0x48, 0x8d, 0x3c, 0x3e};
    asm_bytes(as, bytes, RCS_ARRAY_LEN(bytes));
}

// mov ah, 1
static void asm_set_no_sink_flag(struct asm *as) {
    uint8_t bytes[] = {0xb4, 0x01};
    asm_bytes(as, bytes, RCS_ARRAY_LEN(bytes));
}

// test ah, ah
static void asm_test_no_sink_flag(struct asm *as) {
    uint8_t bytes[] = {0x84, 0xe4};
    asm_bytes(as, bytes, RCS_ARRAY_LEN(bytes));
}

static size_t asm_optimize_jump_instr(struct rcs_vec *label_addrs, struct asm_jump_rec *jump) {
    size_t jump_to_address = *rcs_vec_element(label_addrs, jump->to_label, size_t);

    const size_t JUMP_REL8_INSTR_SIZE = 2;
    const size_t JUMP_REL32_INSTR_SIZE = jump->condition == ASM_NO_CONDITION ? 5 : 6;
    bool rel32_required;
    if (jump->instr_addr_pass2 > jump_to_address)
        // back
        rel32_required = jump->instr_addr_pass2 - jump_to_address > 128 - JUMP_REL8_INSTR_SIZE;
    else
        // forward
        rel32_required = jump_to_address - jump->instr_addr_pass2 > 127 + JUMP_REL8_INSTR_SIZE;

    if (!rel32_required) {
        jump->is_rel32 = false;
        return JUMP_REL32_INSTR_SIZE - JUMP_REL8_INSTR_SIZE;
    }

    return 0;
}

// Returns number of bytes optimized.
static size_t asm_optimize_jumps(struct asm *as) {
    size_t label_ord_i = 0;
    size_t jump_i = 0;
    size_t addr_adjustment = 0;

    while (label_ord_i < as->label_idx_ordered.len) {
        asm_label label_idx = *rcs_vec_element(&as->label_idx_ordered, label_ord_i, asm_label);
        size_t *label_addr = rcs_vec_element(&as->label_addrs, label_idx, size_t);
        assert(*label_addr != ASM_LABEL_NO_ADDRESS);

        while (jump_i < as->jumps.len) {
            struct asm_jump_rec *jump = rcs_vec_element(&as->jumps, jump_i, struct asm_jump_rec);

            if (jump->instr_addr_pass2 >= *label_addr) {
                // if (jump_i != 0)
                //     --jump_i;
                break;
            }

            jump->instr_addr_pass2 = jump->instr_addr_pass1 - addr_adjustment;
            addr_adjustment += asm_optimize_jump_instr(&as->label_addrs, jump);

            ++jump_i;
        }

        *label_addr -= addr_adjustment;
        ++label_ord_i;
    }

    for (; jump_i < as->jumps.len; ++jump_i) {
        struct asm_jump_rec *jump = rcs_vec_element(&as->jumps, jump_i, struct asm_jump_rec);
        jump->instr_addr_pass2 = jump->instr_addr_pass1 - addr_adjustment;
        addr_adjustment += asm_optimize_jump_instr(&as->label_addrs, jump);
    }

    return addr_adjustment;
}

// `jump_to_addr` must be an address of the first byte of some instruction.
// Otherwise behaviour is undefined.
// Fails if the offset exceeds 32-bit signed integer limit.
static size_t calculate_jump_rel_offset(
    struct asm *as,
    size_t instr_addr,
    size_t jump_to_addr,
    size_t instr_size,
    size_t max
) {
    bool back = jump_to_addr <= instr_addr;

    size_t absolute_offset;
    if (back)
        absolute_offset = instr_addr + instr_size - jump_to_addr;
    else
        absolute_offset = jump_to_addr - instr_addr - instr_size;

    if (absolute_offset > max)
        asm_fail(as, RCS_MAKE_ERR(RCS_ERR_JIT_TOO_LONG_JUMP));

    if (back)
        return max - absolute_offset + 1;
    else
        return absolute_offset;
}

static void asm_link(struct asm *as, uint8_t *dst, size_t dst_size) {
    assert(as->code.len != 0);
    assert(as->jumps.len != 0);

    // generated code is divided into the blocks:
    //   +---------------+      +---------------+           +---------------+
    //   | jumpless code | jump | jumpless code | .... jump | jumpless code |
    //   +---------------+      +---------------+           +---------------+
    //   ^               ^                                     last block
    // start            end

    size_t pass1_block_start = 0;
    size_t pass1_block_end;
    size_t dst_offset = 0;

    for (size_t i = 0; i < as->jumps.len; ++i) {
        struct asm_jump_rec *jump = rcs_vec_element(&as->jumps, i, struct asm_jump_rec);
        pass1_block_end = jump->instr_addr_pass1;

        // copy block [jump_{i-1}, jump2_{i}) or [start, jump2_{i})
        size_t block_len = pass1_block_end - pass1_block_start;
        memcpy(
            dst + dst_offset,
            as->code.data + pass1_block_start,
            pass1_block_end - pass1_block_start
        );
        dst_offset += block_len;

        size_t jump_to_addr = *rcs_vec_element(&as->label_addrs, jump->to_label, size_t);
        // rel32 flag is already set (if 32-bit offset is needed) and offset is checked to fit
        // the operand bit width.
        if (jump->is_rel32) {
            size_t rel32 = calculate_jump_rel_offset(
                as,
                jump->instr_addr_pass2,
                jump_to_addr,
                jump->condition == ASM_NO_CONDITION ? 5 : 6,
                0xffffffff
            );

            if (jump->condition == ASM_NO_CONDITION) {
                dst[dst_offset++] = 0xe9;
            } else {
                dst[dst_offset++] = 0x0f;
                dst[dst_offset++] = 0x80 | jump->condition;
            }

            dst[dst_offset++] = (rel32 & 0x000000ff);
            dst[dst_offset++] = (rel32 & 0x0000ff00) >> 8;
            dst[dst_offset++] = (rel32 & 0x00ff0000) >> 16;
            dst[dst_offset++] = (rel32 & 0xff000000) >> 24;
        } else {
            size_t rel8 =
                calculate_jump_rel_offset(as, jump->instr_addr_pass2, jump_to_addr, 2, 0xff);

            if (jump->condition == ASM_NO_CONDITION)
                dst[dst_offset++] = 0xeb;
            else
                dst[dst_offset++] = 0x70 | jump->condition;
            dst[dst_offset++] = rel8;
        }

        pass1_block_start = pass1_block_end + (jump->condition == ASM_NO_CONDITION ? 5 : 6);
    }

    // Copy the last block
    size_t block_len = as->code.len - pass1_block_start;
    memcpy(dst + dst_offset, as->code.data + pass1_block_start, block_len);
    dst_offset += block_len;
    assert(dst_offset == dst_size);
}
