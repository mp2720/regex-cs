CC = gcc
CFLAGS = -Wall -Wextra -pedantic -std=c99

BUILD_DIR = build
INSTALL_DIR = /usr/lib

LIB_NAME = libregex-cs-runtime.so
LIB = ${BUILD_DIR}/${LIB_NAME}
SRCS = $(wildcard *.c)
OBJS = $(SRCS:%.c=$(BUILD_DIR)/%.o)
HEADERS = $(wildcard *.h)

$(BUILD_DIR):
	mkdir -p $@

$(BUILD_DIR)/%.o: %.c $(HEADERS)
	$(CC) $(CFLAGS) -g -fPIC -c $< -o $@

$(LIB): $(BUILD_DIR) $(OBJS)
	$(CC) $(OBJS) -shared -o $@

$(INSTALL_DIR)/$(LIB_NAME): $(LIB)
	install $(LIB) /usr/lib/

all: $(BUILD_DIR) $(LIB)

clean:
	rm -rf $(BUILD_DIR)

install: $(INSTALL_DIR)/$(LIB_NAME)
	
.PHONY: all clean install
