import sys

input_path = 'Assets/Models/LR/model_backup.obj'
output_path = 'Assets/Models/LR/model.obj'

print(f"변환 시작: {input_path} -> {output_path}")

try:
    with open(input_path, 'r') as f_in, open(output_path, 'w') as f_out:
        for line in f_in:
            if line.startswith('v '):
                parts = line.split()
                if len(parts) >= 4:
                    try:
                        x = float(parts[1])
                        y = float(parts[2])
                        z = float(parts[3])
                        # Rotate -90 around Y: (x, y, z) -> (-z, y, x)
                        f_out.write(f"v {-z} {y} {x}\n")
                    except ValueError:
                        f_out.write(line)
                else:
                    f_out.write(line)
            elif line.startswith('vn '):
                parts = line.split()
                if len(parts) >= 4:
                    try:
                        x = float(parts[1])
                        y = float(parts[2])
                        z = float(parts[3])
                        f_out.write(f"vn {-z} {y} {x}\n")
                    except ValueError:
                        f_out.write(line)
                else:
                    f_out.write(line)
            else:
                f_out.write(line)
    print("변환 완료!")
except Exception as e:
    print(f"오류 발생: {e}")
