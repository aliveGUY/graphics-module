#version 330 core

in vec2 TexCoord; // Texture coordinates from vertex shader
out vec4 FragColor;

uniform sampler2D textureSampler;

void main() {
    // Sample the texture and output the color
    FragColor = texture(textureSampler, TexCoord);
}
