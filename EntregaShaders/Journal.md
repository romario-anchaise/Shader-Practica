# Shader Mastery Challenge — Shander

## Abrir la entrega

1. En Unity, abre `Assets/_MyShaders/Scenes/ShaderShowcase.unity`.
2. Pulsa Play y haz clic dentro de Game.
3. Usa las teclas **1–6** para ver cada efecto de cerca, **Tab** para volver a la galería y **H** para ocultar el panel.
4. **WASD** mueve la cámara; **Q/E** cambia la altura; mantén clic derecho para mirar. **Shift** aumenta la velocidad.
5. **R** activa o desactiva el portal. Para probarlo, pulsa 6 y avanza con W hacia la membrana. Activo: te lleva al punto de llegada. Inactivo: no teletransporta.

Todos los patrones son procedurales: no se necesitan paquetes de texturas ni descargas externas.

## Nivel 1 — Aprendiz

### SG_PulseGlow: energía pulsante

**Cadena:** Time → Multiply con Pulse Speed → Sine → Remap de (-1,1) a (0,1) → Multiply con Base Color HDR → Emission. Base Color también alimenta Base Color del Fragment.

**Qué aporta cada nodo:** Time produce un valor creciente; Multiply controla la frecuencia; Sine crea la oscilación; Remap evita valores negativos; el último Multiply convierte la oscilación en energía luminosa. El Volume de la escena tiene Bloom y la cámara tiene postprocesamiento activado.

**Propiedades:** Base Color (HDR), Pulse Speed (2). **Aplicación:** prefab `PulseBeacon`, esfera giratoria de la primera estación.

### SG_Dissolve: disolución con borde incandescente

**Cadena:** UV → Voronoi → Step(Dissolve Amount, ruido) → Alpha. Alpha Clipping está activado y Alpha Clip Threshold queda en **0.5**.

Un segundo Step usa Dissolve Amount + Edge Width. Restar el segundo al primero aísla la franja que todavía es visible. Esa franja multiplicada por Edge Color HDR se conecta a Emission. El script `DissolveDemonstration` anima el valor mediante MaterialPropertyBlock, sin alterar el material compartido.

**Adaptación técnica de la diapositiva:** conectar la misma máscara binaria a Alpha y al umbral daría `clip(0)` en las zonas que deberían desaparecer. Por eso se utiliza un umbral constante de 0.5, y el borde se calcula en el lado visible de la máscara.

**Propiedades:** Dissolve Amount (slider 0–1), Edge Color HDR, Edge Width, Base Color. **Aplicación:** prefab `DissolvingRelic`, cubo que gira y se desintegra.

### SG_Water: agua estilizada

**Cadena:** Time × Flow Speed → Offset de Tiling And Offset → UV de Gradient Noise → Normal From Height → Normal (Tangent Space).

Water Tint alimenta Base Color; Split extrae A para Alpha. Surface Type es Transparent y Smoothness es 0.9. Normal From Height cambia la respuesta a la luz; los vértices del plano permanecen inmóviles.

**Propiedades:** Flow Speed (0.1, 0.05), Water Tint (azul, alpha 0.8). **Aplicación:** plano del prefab `WaterBasin`. El ejercicio anterior `WaterDemo` se conserva dentro de `Level1/Water`.

## Nivel 2 — Artesano

### Arquetipo A: SG_ForceField

Fresnel mide la orientación respecto de la cámara y destaca el borde de la esfera. Un Voronoi con UV desplazadas por Time genera celdas que se mueven; Step selecciona sus contornos. Se suma una contribución moderada de celdas al Fresnel, se limita el resultado con Saturate y se usa tanto para Emission HDR como para Alpha.

**Propiedades expuestas:** Shield Color, Cell Density, Rim Power, Flow Speed, Base Tint. **Aplicación:** prefab `ForceFieldGenerator`, membrana transparente alrededor de un núcleo protegido. Se renderiza la cara frontal para evitar que el Fresnel de la cara posterior rellene la esfera de luz.

### Arquetipo B: SG_Hologram

Time desplaza las UV verticalmente. Split toma UV.y; Multiply con Scan Density y Fraction generan bandas repetidas; Step fija el grosor de las líneas. Fresnel realza la silueta. Una segunda rama temporal, Sine → Step → Glitch Strength → One Minus, produce breves caídas de señal, que afectan a emisión y opacidad.

**Propiedades expuestas:** Holo Color HDR, Scan Speed, Scan Density, Rim Power, Opacity, Glitch Speed, Glitch Strength y Base Tint. **Aplicación:** prefab `HolographicBeacon`, cápsula giratoria semitransparente.

## Nivel 3 — Maestro

### Nombre y propósito: Umbral de Marea

**Problema visual que resuelve:** el jugador necesita distinguir un portal utilizable de uno inactivo antes de atravesarlo. La membrana activa presenta vetas luminosas turquesa; la inactiva se oscurece. El estado visual y la autorización de teletransporte usan el mismo valor `Readiness`, por lo que el shader comunica una regla real del juego.

**Técnicas combinadas:**

1. Desplazamiento UV en dos direcciones, con Tide Speed y Counterflow.
2. Dos campos de Simple Noise a diferentes escalas, multiplicados para generar turbulencia.
3. Smoothstep para seleccionar vetas, más suma, multiplicación y Saturate para controlar el patrón.
4. Fresnel para los bordes y Time/Sine/Remap para una pulsación suave.
5. Lerp entre Dormant Color y Active Color según Readiness.

**Propiedades expuestas:** Tide Speed, Counterflow, Rim Power, Pulse Speed, Readiness (0–1), Dormant Color HDR, Active Color HDR y Energy.

**Integración real en este proyecto:** `Assets/_MyShaders/Prefabs/TideGate.prefab` contiene el marco, la membrana con `M_TideGate`, un volumen Trigger, Rigidbody cinemático y el componente `TideGate`. La instancia de la escena referencia un punto de llegada. El jugador tiene CharacterController. No es únicamente una esfera de prueba.

**Límite de la integración:** este proyecto partía de una plantilla sin prefabs ni mecánicas del juego grupal. Se añadió este portal funcional aquí. Si el proyecto grupal tiene otra identidad visual, deberá adaptarse el concepto y trasladarse el prefab a ese proyecto; no se presenta esta galería como una integración ya realizada en un repositorio grupal distinto.

### ¿Qué cambió al aplicarlo al prefab en vez de a la esfera?

En la esfera, Fresnel ocupa buena parte del contorno curvo. En la membrana plana del portal, mirando de frente, Fresnel es débil: las vetas de ruido y la pulsación deben sostener la lectura del efecto. La superficie rectangular también tiene una distribución UV distinta y exige ajustar escala, opacidad y encuadre. Se mantuvieron las UV normalizadas de una Quad para que el patrón no dependa de la escala física del marco. La membrana es de dos caras para que el portal se vea desde ambos lados.

## ¿Qué nodos permitieron los mejores efectos y por qué?

- **Fresnel:** separa la silueta del fondo en escudo, holograma y portal.
- **Normal From Height:** convierte ruido escalar en una respuesta de iluminación que se percibe como agua.
- **Step y Smoothstep:** controlan respectivamente cortes duros y transiciones suaves; son esenciales para disolución, scanlines y vetas.
- **Time + Tiling And Offset:** animan los materiales sin texturas ni scripts por cada onda.
- **Sine + Remap:** producen una pulsación periódica y acotada, adecuada para emisión HDR.

## Dev Kata S11 — El Material Imposible

**Cristal de marea:** una membrana de cristal que contiene corrientes líquidas de luz sin cambiar la forma de su soporte. Es el material del Umbral de Marea. La forma rígida pertenece al prefab; la corriente interior se construye con dos ruidos que fluyen en direcciones opuestas. El brillo cambia con su disponibilidad como portal.

El GIF `Evidencias/MaterialImposible_CristalDeMarea.gif` muestra el material animado y queda preparado para adjuntarlo al foro. No se publica automáticamente.

## Evidencias

- `Evidencias/00_Galeria.png`: conjunto de los seis efectos.
- `Evidencias/01_*.png` a `06_*.png`: primeros planos renderizados por Unity usando los materiales del proyecto.
- `Evidencias/Grafos/`: diagramas SVG/PNG exportados desde las conexiones reales de cada `.shadergraph`. Se identifican como diagramas exportados, no como capturas de la interfaz.
- `Evidencias/Validacion.txt`: resultado de la comprobación en Play.
- `Evidencias/MaterialImposible_CristalDeMarea.gif`: animación del material original para la Kata.

Para una captura directa de la interfaz: abre cada `.shadergraph` con doble clic, sitúa el cursor en el lienzo, pulsa A para encuadrar todos los nodos y toma la captura. Antes de entregar ediciones manuales, utiliza Save Asset en el grafo y guarda la escena.

## Presentación de un minuto

«Nuestro material original se llama Umbral de Marea. Resuelve la lectura del estado de un portal: luminoso cuando puede usarse, oscuro cuando está inactivo. Combinamos dos UV animadas, ruido procedural, Smoothstep, Fresnel y pulsación temporal. Readiness conecta la apariencia con la lógica del prefab. Al cambiar de una esfera a una superficie plana tuvimos que reforzar las vetas, porque el Fresnel casi desaparece al mirarla de frente. Ahora desactivo el portal con R; lo activo y lo atravieso para demostrar la integración.»

## Fuente y entrega pendiente

Construido siguiendo la práctica S11 Shader Mastery Challenge proporcionada por el profesor: niveles 1–3 (páginas 5–9), showcase (página 10) y checklist (página 11). Documento consultado: `S11_Practica_Shader_Mastery_Challenge.pdf`.

Repositorio: https://github.com/romario-anchaise/Shader-Practica. La rama de trabajo es `feature/Shaders`; `main` contiene la versión inicial de la entrega. El GIF se entrega para que el alumno lo publique en su foro. Quedan pendientes las capturas directas de la interfaz de Unity.
