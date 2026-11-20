# Cartones de bingo en Godot 4.7 (.NET): guía de aprendizaje

Prototipo para aprender: una mano de cartones de bingo (90 bolas, 3×9) que
se pueden coger, arrastrar y soltar en la **mesa** o en el **descarte**.
2D con un shader que da un efecto de inclinación en 3D, al estilo de Balatro.

## Cómo abrirlo

1. Abre `Godot_v4.7.2-stable_mono_win64.exe` → **Importar** → elige `project.godot`.
2. Botón **Compilar** (martillo, arriba a la derecha) la primera vez.
3. **F5** para ejecutar.

## Mapa de archivos

| Archivo | Qué es | Concepto de Godot que enseña |
|---|---|---|
| `scripts/bingo/CartonBingo.cs` | Datos del cartón y generador | C# puro, **sin Godot**. La lógica separada de la vista |
| `scripts/ui/CaraCarton.cs` | Dibuja el cartón | Dibujo inmediato con `_Draw()`, `StyleBoxFlat`, fuentes |
| `escenas/carton.tscn` + `scripts/ui/CartonView.cs` | Un cartón interactivo | Escenas reutilizables, `SubViewport`, señales, `Tween`, entrada de ratón |
| `shaders/inclinacion.gdshader` | Efecto 3D falso | Shaders `canvas_item`, `uniform`, `fragment()` |
| `scripts/ui/ZonaCartones.cs` | Base de Mano/Mesa/Descarte | Herencia de nodos, `[Export]` |
| `scripts/ui/Mano.cs` | Abanico | Matemáticas de colocación |
| `scripts/ui/Mesa.cs`, `Descarte.cs` | Rejilla y montón | Lo mismo con otra fórmula |
| `scripts/Juego.cs` + `escenas/juego.tscn` | Escena principal | `PackedScene.Instantiate`, `async`/`ToSignal` |

## Conceptos clave

### 1. Todo son nodos en un árbol, y una escena es un árbol guardado
`carton.tscn` es una escena pequeña que se **instancia** muchas veces desde
`Juego.cs` (`EscenaCarton.Instantiate<CartonView>()`). Abre los `.tscn` en el
editor y mira el panel **Escena** (el árbol de nodos) y el **Inspector**.

### 2. ¿`Control` o `Node2D`?
- `Control` = nodos de interfaz: traen ratón (`MouseEntered`, `_GuiInput`),
  anclas y contenedores. **Lo usamos aquí**, porque una mano de cartas es
  casi interfaz.
- `Node2D` = objetos de un "mundo" 2D (personajes, física). Para recibir el
  ratón necesitan un `Area2D` con colisión.

### 3. El truco del SubViewport (y el puente hacia el 3D)
```
Carton (Control)
 ├─ Render (SubViewport)   ← "pantalla invisible"
 │   └─ Cara (CaraCarton)  ← dibuja el cartón ahí
 └─ Visual (TextureRect)   ← muestra esa pantalla como textura + shader
```
El cartón se dibuja en una textura, y esa textura se muestra con un shader.
**La misma textura se puede poner sobre un plano 3D**: así se pasará a 3D
sin rehacer el dibujo.

### 4. Señales: los objetos avisan, no mandan
`CartonView` emite `Soltado(carton, posicion)` y no sabe nada de mesas.
`Juego` escucha (`carton.Soltado += AlSoltarCarton`) y decide. Es el patrón
más importante de Godot. Se declaran con `[Signal] delegate void XxxEventHandler(...)`.

### 5. Tweens: animar sin escribir el bucle
`CreateTween().TweenProperty(this, "position", destino, 0.3f)` lleva la
propiedad hasta el valor en 0,3 s. `SetParallel()` anima varias a la vez y
`Kill()` cancela la anterior para que no se peleen.

### 6. `_GuiInput` frente a `_Input`
- `_GuiInput`: solo si el ratón está **sobre** ese Control → para "empezar a coger".
- `_Input`: llega **siempre** → para "soltar", aunque el ratón se haya
  escapado del cartón al moverlo rápido.

### 7. Trampas típicas (están comentadas en el código)
- **ZIndex ≠ quién recibe el ratón.** `ZIndex` cambia el orden de dibujo; el
  ratón va por el orden en el árbol (el último hijo está "encima").
- **Material compartido.** Un material de una escena se comparte entre todas
  sus instancias. Por eso en `carton.tscn` tiene `resource_local_to_scene = true`.
  Sin eso, al inclinar un cartón se inclinarían todos.
- **Parpadeo del hover.** Si el cartón sube al pasar el ratón, el ratón sale
  de él, baja, vuelve a entrar... En `Mano.cs` crece hacia arriba manteniendo
  el borde inferior fijo.
- **Lerp y FPS.** `Lerp(a, b, 0.1f)` cada frame va más rápido a 144 FPS que a 60.
  Se usa `1 - Exp(-velocidad * delta)` (función `Suavizado`).
- **Nombres de propiedad.** En `TweenProperty` van en snake_case de Godot
  (`"position"`), no el nombre de C# (`Position`).
- **Antes de `_Ready`** no existen los hijos. `Configurar()` guarda los datos
  y los aplica cuando el nodo está listo (`IsNodeReady()`).

### 8. El shader, en una frase
Para cada píxel se lanza un rayo desde una cámara imaginaria, se corta con
el plano del cartón girado `rot_x`/`rot_y` grados y se lee el color de ese
punto. Abre `inclinacion.gdshader`, selecciona el nodo `Visual` en
`carton.tscn` y cambia los `uniform` en el Inspector para verlo en vivo.

## Ejercicios para practicar
1. Cambia en el Inspector (nodo `Mano` de `juego.tscn`) `Curvatura`,
   `GradosPorCarton` y `EscalaHover`, y fíjate en el efecto.
2. Baja `distancia` en el shader a 1.5: más perspectiva.
3. **Bombo:** un botón que saque un número del 1 al 90 y llame a
   `Datos.Marcar(n)` + `Redibujar()` en los cartones de la mesa (ya existen
   las dos funciones, y `CaraCarton` ya dibuja los marcados).
4. Detectar **línea** y **bingo** en `CartonBingo` (lógica pura, sin Godot).
5. Que al soltar un cartón en la mano se inserte en la posición del ratón,
   no al final.

## Versión 3D

Escena `escenas/juego3d.tscn` (un solo nodo) + `scripts/Juego3D.cs` y
`scripts/ui3d/`. **Todo se crea por código**, sin editor. Ejecutar:
```powershell
dotnet build; & "C:\godot\CSharp\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe" --path . escenas/juego3d.tscn
```

| Archivo | Qué enseña |
|---|---|
| `Juego3D.cs` | Montar una escena 3D por código: `WorldEnvironment` (cielo, luz ambiental, tonemapping, SSAO), luces direccionales con sombras, `Camera3D`, UI 2D encima con `CanvasLayer`. Rayo del ratón (`ProjectRayOrigin`/`ProjectRayNormal`) y `Plane.IntersectsRay`. `_UnhandledInput` frente a `_Input` |
| `ui3d/Carton3D.cs` | Mallas (`BoxMesh`, `PlaneMesh`), `StandardMaterial3D`, "hornear" un SubViewport en una `ImageTexture` con mipmaps, intersección rayo-rectángulo pasando a coordenadas locales (`AffineInverse`), tweens de `Quaternion` |
| `ui3d/Zona3D.cs` y hijas | Igual que en 2D, pero las posiciones son `Vector3` y las rotaciones `Quaternion` |

Qué se reutiliza de 2D sin cambios: `CartonBingo` (datos) y `CaraCarton`
(el dibujo del cartón). Qué desaparece: el shader de inclinación, porque
ahora la inclinación es rotación 3D real y la luz y las sombras las pone el motor.
