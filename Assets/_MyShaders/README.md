# Shader Mastery — Shander

Abre **Scenes/ShaderShowcase.unity** y pulsa **Play**. Haz clic en Game para usar los controles.

| Tecla | Acción |
|---|---|
| 1–6 | Primer plano del shader elegido |
| Tab | Vista de toda la galería |
| WASD | Desplazamiento |
| Q / E | Bajar / subir |
| Clic derecho + ratón | Mirar |
| Shift | Moverse más rápido |
| R | Activar / desactivar el portal |
| H | Mostrar / ocultar el panel |

Para ver los nodos, abre con doble clic un archivo `.shadergraph`; sitúa el cursor en el lienzo y pulsa **A**. Las propiedades expuestas también se pueden cambiar desde el material en el Inspector.

**Nivel 1:** SG_PulseGlow, SG_Dissolve y Level1/Water/SG_Water.

**Nivel 2:** SG_ForceField y SG_Hologram.

**Nivel 3:** SG_TideGate, aplicado al prefab funcional Prefabs/TideGate. Su membrana representa un cristal de marea: vetas de luz líquida en un soporte rígido. La propiedad Readiness refleja la disponibilidad real del teletransporte.

En la carpeta **EntregaShaders**, situada en la raíz del proyecto, están el Journal, las imágenes, los diagramas de los grafos y el GIF de la Kata.

El material de agua anterior y su escena se conservaron en **Level1/Water**. Los identificadores `.meta` se mantienen al reorganizar las carpetas.

El shader de disolución utiliza Alpha Clip Threshold fijo en 0.5 para que el recorte funcione correctamente. Su animación está en DissolveDemonstration; se puede desactivar en el Inspector para editar manualmente Dissolve Amount.
