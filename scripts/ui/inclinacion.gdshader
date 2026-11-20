// Shader de "falso 3D" para un elemento 2D (al estilo Balatro).
//
// Idea: el rectángulo 2D se queda quieto en pantalla, pero en cada píxel
// calculamos qué parte del cartón se vería si el cartón fuera un plano
// girado en 3D y lo mirásemos con una cámara en perspectiva.
//
// Para cada píxel:
//   1. Lanzamos un rayo desde una cámara imaginaria que pasa por ese píxel.
//   2. Calculamos dónde corta ese rayo al plano del cartón (ya girado).
//   3. Convertimos el punto de corte a coordenadas de textura (uv).
//   4. Si cae fuera del cartón, el píxel es transparente.
//
// Los parámetros "uniform" se pueden cambiar desde el inspector o desde
// C# con material.SetShaderParameter("nombre", valor).

shader_type canvas_item;

uniform float rot_x = 0.0;            // grados: giro sobre el eje horizontal
uniform float rot_y = 0.0;            // grados: giro sobre el eje vertical
uniform float distancia = 3.0;        // distancia de la cámara (en altos de cartón). Menor = más perspectiva
uniform float aspecto = 2.386;        // ancho / alto del cartón (420 / 176)
uniform float margen = 0.08;          // encoge el cartón para que al girar no se salga del rectángulo
uniform float brillo = 0.0;           // intensidad del reflejo de luz
uniform vec2 luz = vec2(0.5, 0.5);    // posición del reflejo (0..1)

varying vec4 modulacion;

void vertex() {
	// En vertex(), COLOR contiene el "modulate" del nodo. Lo guardamos
	// para aplicarlo al final, porque en fragment() vamos a sobrescribir COLOR.
	modulacion = COLOR;
}

void fragment() {
	float a = radians(rot_x);
	float b = radians(rot_y);
	float ca = cos(a);
	float sa = sin(a);
	float cb = cos(b);
	float sb = sin(b);

	// Ejes del cartón tras girarlo (primero en X y luego en Y).
	vec3 eje_u = vec3(cb, 0.0, -sb);              // "derecha" del cartón
	vec3 eje_v = vec3(sa * sb, ca, sa * cb);      // "abajo" del cartón
	vec3 normal = vec3(ca * sb, -sa, ca * cb);    // hacia dónde mira

	// Punto de la pantalla para este píxel (centrado en 0, alto = 1).
	vec2 s = (UV - 0.5) * vec2(aspecto, 1.0) * (1.0 + margen);

	// Rayo desde la cámara hasta ese punto, y corte con el plano del cartón.
	vec3 camara = vec3(0.0, 0.0, distancia);
	vec3 dir = vec3(s, 0.0) - camara;
	float t = -dot(camara, normal) / dot(dir, normal);
	vec3 p = camara + t * dir;

	// Coordenadas dentro del cartón (0..1).
	vec2 uv = vec2(dot(p, eje_u) / aspecto, dot(p, eje_v)) + 0.5;

	if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) {
		discard; // fuera del cartón: no se pinta
	}

	vec4 color = texture(TEXTURE, uv);

	// Suavizado de bordes (antialiasing): fwidth dice cuánto cambia uv
	// entre píxeles vecinos; el último píxel del borde se hace semitransparente.
	vec2 al_borde = min(uv, 1.0 - uv) / max(fwidth(uv), vec2(1e-5));
	color.a *= clamp(min(al_borde.x, al_borde.y), 0.0, 1.0);

	// Reflejo de luz simple.
	float d = distance(uv * vec2(aspecto, 1.0), luz * vec2(aspecto, 1.0));
	color.rgb += brillo * pow(max(0.0, 1.0 - d), 3.0);

	COLOR = color * modulacion;
}
