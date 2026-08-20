shader_type canvas_item;

// Rarity presets: 0 = Custom, 2 = Green, 3 = Blue, 4 = Purple, 5 = Gold
uniform int rarity_preset : hint_range(0, 5) = 5;
uniform vec4 custom_shine_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);

uniform float shine_speed : hint_range(0.1, 5.0) = 1.5;
uniform float shine_size : hint_range(0.01, 0.5) = 0.12;
uniform float shine_angle : hint_range(-3.14, 3.14) = 0.785; // 45 degrees
uniform float pause_duration : hint_range(0.0, 5.0) = 1.5;

vec4 get_preset_color(int preset) {
    switch (preset) {
        case 2: return vec4(0.0, 1.0, 0.3, 1.0);   // Max Vibrant Green
        case 3: return vec4(0.0, 0.6, 1.0, 1.0);   // Max Vibrant Blue
        case 4: return vec4(0.7, 0.0, 1.0, 1.0);   // Max Vibrant Purple
        case 5: return vec4(1.0, 0.84, 0.0, 1.0);  // Vibrant Gold
        default: return custom_shine_color;
    }
}

void fragment() {
    vec4 tex_color = texture(TEXTURE, UV);
    vec4 active_shine_color = get_preset_color(rarity_preset);
    
    // Cycle timing with configurable pause
    float cycle_time = shine_speed * TIME;
    float total_cycle = 1.0 + (pause_duration * shine_speed);
    float progress = mod(cycle_time, total_cycle);
    
    // UV Rotation for angled sweep
    vec2 center = UV - vec2(0.5);
    float cos_angle = cos(shine_angle);
    float sin_angle = sin(shine_angle);
    vec2 rotated_uv = vec2(
        center.x * cos_angle - center.y * sin_angle,
        center.x * sin_angle + center.y * cos_angle
    ) + vec2(0.5);

    // Calculate light band
    float shine_line = progress - 0.5;
    float dist = abs(rotated_uv.x - shine_line);
    float shine_intensity = smoothstep(shine_size, 0.0, dist);

    // Mix sheen into base texture while respecting pixel alpha
    vec3 blended_rgb = mix(tex_color.rgb, active_shine_color.rgb, shine_intensity * active_shine_color.a);
    COLOR = vec4(blended_rgb, tex_color.a);
}