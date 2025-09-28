CC = gcc
CFLAGS = -Wall -Wextra -pedantic -std=c99

BUILD_DIR = build

LIB = ${BUILD_DIR}/libregex-cs-runtime.so
SRCS = $(wildcard *.c)
OBJS = $(SRCS:%.c=$(BUILD_DIR)/%.o)
HEADERS = $(wildcard *.h)

all: $(BUILD_DIR) $(LIB)

$(BUILD_DIR):
	mkdir -p $@

$(BUILD_DIR)/%.o: %.c $(HEADERS)
	$(CC) $(CFLAGS) -g -fPIC -c $< -o $@

$(LIB): $(OBJS)
	$(CC) $(OBJS) -shared -o $@

clean:
	rm -rf $(BUILD_DIR)

.PHONY: all clean
