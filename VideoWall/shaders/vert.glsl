#version 450
#pragma shader_stage(vertex)

layout(location = 0) in vec2 inPosition;
layout(location = 1) in vec3 inColor;

layout(location = 0) out vec3 color;

void main(){
    gl_Position = vec4(inPosition, 0, 1);
    color = inColor;
}