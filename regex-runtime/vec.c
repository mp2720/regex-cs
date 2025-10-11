#include "vec.h"

#include "stdlib.h"
#include <assert.h>
#include <errno.h>
#include <string.h>

#define GROW(x) ((x) * 3 / 2)

const struct rcs_vec rcs_zero_vec = {.data = NULL, .len = 0, .capacity = 0, .element_size = 0};

RCS_NODISCARD
rcs_error rcs_vec_init(struct rcs_vec *vec, size_t element_size, size_t init_capactity) {
    assert(vec != NULL);
    assert(element_size != 0);
    assert(init_capactity != 0);

    vec->element_size = element_size;
    vec->len = 0;
    vec->capacity = init_capactity < 2 ? 2 : init_capactity;
    vec->data = malloc(vec->capacity * element_size);
    if (vec->data == NULL)
        return RCS_MAKE_ERR_LIBC(errno);
    return RCS_OK;
}

RCS_NODISCARD rcs_error rcs_vec_push(struct rcs_vec *vec, void *element) {
    return rcs_vec_push_many(vec, element, 1);
}

RCS_NODISCARD
rcs_error rcs_vec_push_many(struct rcs_vec *vec, void *elements, size_t elements_count) {
    assert(vec != 0);
    assert(elements != 0);
    assert(elements_count != 0);

    if (vec->len + elements_count <= vec->capacity) {
        memcpy(
            vec->data + vec->len * vec->element_size,
            elements,
            elements_count * vec->element_size
        );
        vec->len += elements_count;
        return RCS_OK;
    }

    size_t new_capactity = GROW(vec->capacity);
    if (new_capactity - vec->len < elements_count)
        new_capactity = vec->capacity + elements_count;

    unsigned char *data_realloc = realloc(vec->data, new_capactity * vec->element_size);
    if (data_realloc == NULL)
        return RCS_MAKE_ERR_LIBC(errno);

    vec->data = data_realloc;
    vec->capacity = new_capactity;
    memcpy(vec->data + vec->len * vec->element_size, elements, elements_count * vec->element_size);
    vec->len += elements_count;

    return RCS_OK;
}

void rcs_vec_free_data(struct rcs_vec *vec) {
    assert(vec != NULL);

    free(vec->data);
    *vec = rcs_zero_vec;
}
