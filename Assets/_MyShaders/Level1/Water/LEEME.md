# SG_Water — agua estilizada

Abre `WaterDemo.unity` y pulsa **Play**. El plano usa el material `M_Water`.
Abre `SG_Water.shadergraph` con doble clic para ver los nodos.

## Cómo explicar el grafo

1. **Time**, salida Time: segundos transcurridos, para mover las ondas continuamente.
2. **Flow Speed**, Vector2 `(0.1, 0.05)`: velocidad horizontal y vertical de las coordenadas UV.
3. **Multiply**: multiplica el tiempo por esa velocidad; produce el desplazamiento UV.
4. **Tiling And Offset**: recibe el desplazamiento en Offset y mueve las UV. Tiling queda en `(1, 1)`.
5. **Gradient Noise**: genera alturas de ondas sin imágenes externas. Scale queda en `10`.
6. **Normal From Height**: convierte esas alturas en normales que responden a la luz; salida Tangent conectada a Normal (Tangent Space). Strength está en `0.35` para apreciar el relieve en el plano de demostración.
7. **Water Tint**: color azul con alpha `0.8`, conectado a Base Color. **Split** extrae su canal A para conectarlo a Alpha.
8. **Smoothness**: `0.9`, para reflejos más definidos. **Surface Type**: Transparent, con mezcla Alpha.

La superficie del plano permanece inmóvil: se animan las coordenadas con las que se calcula el ruido. No hay desplazamiento de vértices ni texturas externas.

## Ajustes

Selecciona `M_Water` para editar Flow Speed y Water Tint.
Una velocidad `(0, 0)` detiene las ondas. El alpha de Water Tint controla la transparencia.
Dentro del grafo puedes modificar Scale de Gradient Noise y Strength de Normal From Height.

El fondo de baldosas y los bordes solo ayudan a observar la transparencia y los reflejos.
Este ejercicio corresponde al shader de agua. El checkpoint de la diapositiva también pide otros dos shaders en objetos diferentes.
