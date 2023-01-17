shopt -s globstar
for i in **/*.glsl; do
    fileName="${i%.*}"
    glslc "$i" -o "${fileName}.spv"
done