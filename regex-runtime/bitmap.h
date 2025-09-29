#ifndef REGEX_CS_RUNTIME_BITMAP
#define REGEX_CS_RUNTIME_BITMAP

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>
#include <string.h>

#if defined(__x86_64__)
typedef uint64_t rcs_bitmap_word;
#define RCS_BITMAP_WORD_BIT_WIDTH 64
#define RCS_BITMAP_WORD_BYTE_WIDTH 8
#elif defined(__i386__)
typedef uint32_t rcs_bitmap_word;
#define RCS_BITMAP_WORD_BIT_WIDTH 32
#define RCS_BITMAP_WORD_BYTE_WIDTH 4
#else
typedef uint32_t rcs_bitmap_word;
#endif

#define RCS_DIV_CEILING(x, y) (((x) + (y) - 1) / (y))

#define RCS_BITMAP_LEN_WORDS(bits) RCS_DIV_CEILING(bits, RCS_BITMAP_WORD_BIT_WIDTH)

static bool rcs_bitmap_get(const rcs_bitmap_word *bm, size_t index) {
    size_t word_index = index / RCS_BITMAP_WORD_BIT_WIDTH;
    size_t bit_index = index % RCS_BITMAP_WORD_BIT_WIDTH;
    return bm[word_index] & (1 << bit_index);
}

static void rcs_bitmap_set(rcs_bitmap_word *bm, size_t index) {
    size_t word_index = index / RCS_BITMAP_WORD_BIT_WIDTH;
    size_t bit_index = index % RCS_BITMAP_WORD_BIT_WIDTH;
    bm[word_index] |= (1 << bit_index);
}

static void rcs_bitmap_clear(rcs_bitmap_word *bm, size_t index) {
    size_t word_index = index / RCS_BITMAP_WORD_BIT_WIDTH;
    size_t bit_index = index % RCS_BITMAP_WORD_BIT_WIDTH;
    bm[word_index] &= ~(1 << bit_index);
}

static void rcs_bitmap_clear_all(rcs_bitmap_word *bm, size_t bm_len) {
    memset(bm, 0, bm_len * sizeof *bm);
}

#endif
