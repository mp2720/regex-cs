#ifndef REGEX_CS_RUNTIME_VEC
#define REGEX_CS_RUNTIME_VEC

#include "api.h"
#include "common.h"
#include <assert.h>
#include <stddef.h>
#include <stdlib.h>

struct rcs_vec {
    unsigned char *data;
    size_t element_size;

    // multiply by element_size to get bytes number

    size_t len;
    size_t capacity;
};

// Assign unitialized vector to this value, so `rcs_vec_free_data()` call on it won't cause an UB.
extern const struct rcs_vec rcs_zero_vec;

RCS_NODISCARD
rcs_error rcs_vec_init(struct rcs_vec *vec, size_t element_size, size_t init_capacity);

RCS_NODISCARD rcs_error rcs_vec_push(struct rcs_vec *vec, void *element);

RCS_NODISCARD rcs_error
rcs_vec_push_many(struct rcs_vec *vec, void *elements, size_t elements_count);

#define rcs_vec_element(vec, index, type) \
    (assert((index) < (vec)->len), (type *)((vec)->data + (index) * (vec)->element_size))

void rcs_vec_free_data(struct rcs_vec *vec);

#endif
