---
trigger: always_on
description: "Regla obligatoria de localización multilingüe completa para todos los catálogos, opciones y desplegables del sistema esd360"
---

# Regla de Localización Completa de Catálogos (i18n)

1. **Invariante de Traducción Total:** Ningún menú desplegable, selector de elementos normativos (como elementos S20.20), categorías, magnitudes, unidades, frecuencias o estatus debe mostrarse en un único idioma fijo.
2. **Sincronización Multilingüe:** Todos los catálogos del sistema deben contar con traducciones oficiales en todos los 6 idiomas del sistema (`es`, `en`, `de`, `it`, `ro`, `zh`).
3. **Mapeo Canónico:** Los elementos desplegables en la interfaz deben mostrar el texto traducido al usuario mientras preservan el identificador o clave canónica internamente para la persistencia en base de datos.
