shader_type spatial;
render_mode cull_back, diffuse_burley;

// Cuadricula infinita dibujada por posicion en el mundo.
// Sirve de referencia de movimiento: ves las lineas pasar y sabes que avanzas.
uniform vec3 color_base : source_color = vec3(0.16, 0.18, 0.21);
uniform vec3 color_linea : source_color = vec3(0.30, 0.45, 0.65);
uniform vec3 color_linea_mayor : source_color = vec3(0.45, 0.70, 0.95);
uniform float tam_celda = 4.0;   // metros por celda

varying vec3 mundo;

void vertex() {
    mundo = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float rejilla(vec2 coord) {
    vec2 g = abs(fract(coord - 0.5) - 0.5) / fwidth(coord);
    return 1.0 - min(min(g.x, g.y), 1.0);
}

void fragment() {
    float chica = rejilla(mundo.xz / tam_celda);
    float grande = rejilla(mundo.xz / (tam_celda * 10.0));
    vec3 col = mix(color_base, color_linea, chica * 0.6);
    col = mix(col, color_linea_mayor, grande);
    ALBEDO = col;
    ROUGHNESS = 0.9;
}
