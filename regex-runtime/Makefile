CC = gcc
CFLAGS = -Wall -Wextra -pedantic -std=c99 -O3 -flto -g

BUILD_DIR = build
INSTALL_DIR = /usr/lib

LIB_NAME = libregex-cs-runtime.so
LIB = ${BUILD_DIR}/${LIB_NAME}
SRCS = $(wildcard *.c)
HEADERS = $(wildcard *.h)

ARCH ?= $(shell uname -m)
ifeq ($(ARCH), x86_64)
	SRCS += $(wildcard amd64/*.c)
	HEADERS += $(wildcard amd64/*.h)
else
	SRCS += $(wildcard noarch/*.c)
	HEADERS += $(wildcard noarch/*.h)
endif

OBJS = $(SRCS:%.c=$(BUILD_DIR)/%.o)

$(LIB): $(BUILD_DIR) $(OBJS)
	$(CC) $(OBJS) -shared -o $@

$(BUILD_DIR):
	mkdir -p $@

$(BUILD_DIR)/%.o: %.c $(HEADERS)
	mkdir -p $(dir $@)
	$(CC) $(CFLAGS) -fPIC -c $< -o $@

$(INSTALL_DIR)/$(LIB_NAME): $(LIB)
	install $(LIB) /usr/lib/

all: $(BUILD_DIR) $(LIB)

clean:
	rm -rf $(BUILD_DIR)

install: $(INSTALL_DIR)/$(LIB_NAME)

.PHONY: all clean install
