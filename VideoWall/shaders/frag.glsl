#version 450
#pragma shader_stage(fragment)

layout(location = 0) in vec3 vertexColor;

layout(location = 0) out vec4 color;

void main(){
    color = vec4(vertexColor, 1);
}