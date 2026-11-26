ifdef PS4_PAYLOAD_SDK
    include $(PS4_PAYLOAD_SDK)/toolchain/orbis.mk
else
    $(error PS4_PAYLOAD_SDK is undefined)
endif

TARGET_ELF := GotoHome.elf
TARGET_BIN := GotoHome.bin
CFLAGS := -std=c11 -Wall -Wextra -Werror -O2

.PHONY: all clean
all: $(TARGET_BIN)

$(TARGET_ELF): main.c
	$(CC) $(CFLAGS) -o "$@" "$<"

$(TARGET_BIN): $(TARGET_ELF)
	cp "$<" "$@"

clean:
	rm -f "$(TARGET_ELF)" "$(TARGET_BIN)"
