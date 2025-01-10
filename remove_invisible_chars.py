import sys

def remove_invisible_chars(file_path):
    try:
        with open(file_path, 'rb') as file:
            content = file.read()

        original_size = len(content)
        original_content = content

        # Remove Byte Order Mark (BOM) if present
        bom_removed = False
        if content.startswith(b'\xef\xbb\xbf'):
            content = content[3:]
            bom_removed = True

        # Define invisible characters to remove
        invisible_chars = b'\x00\x01\x02\x03\x04\x05\x06\x07\x08\x0b\x0c\x0e\x0f\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f'

        # Remove invisible characters from start and end
        content_stripped = content.strip(invisible_chars)

        # Count removed characters
        start_removed = len(content) - len(content.lstrip(invisible_chars))
        end_removed = len(content.rstrip(invisible_chars)) - len(content_stripped)

        # Convert to string
        content_str = content_stripped.decode('utf-8')

        # Only write if changes were made
        if content != original_content:
            with open(file_path, 'w', encoding='utf-8', newline='\n') as file:
                file.write(content_str)

            # Report changes
            changes = []
            if bom_removed:
                changes.append("BOM removed from start of file")
            if start_removed > 0:
                changes.append(f"{start_removed} invisible character(s) removed from start")
            if end_removed > 0:
                changes.append(f"{end_removed} invisible character(s) removed from end")

            print(f"File processed successfully: {file_path}")
            print("Changes made:")
            for change in changes:
                print(f"- {change}")
        else:
            print(f"No changes needed for {file_path}")

    except Exception as e:
        print(f"Error processing {file_path}: {e}")

if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("Usage: python remove_invisible_chars.py <file_path>")
    else:
        remove_invisible_chars(sys.argv[1])