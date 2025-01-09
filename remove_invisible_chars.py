import sys

def remove_invisible_chars(file_path):
    try:
        with open(file_path, 'rb') as file:
            content = file.read()

        # Remove Byte Order Mark (BOM) if present
        if content.startswith(b'\xef\xbb\xbf'):
            content = content[3:]

        # Remove other invisible characters at the start
        content = content.lstrip(b'\x00\x01\x02\x03\x04\x05\x06\x07\x08\x0b\x0c\x0e\x0f\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f')

        with open(file_path, 'wb') as file:
            file.write(content)

        print(f"Invisible characters removed from {file_path}")

    except Exception as e:
        print(f"Error processing {file_path}: {e}")

if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("Usage: python remove_invisible_chars.py <file_path>")
    else:
        remove_invisible_chars(sys.argv[1])