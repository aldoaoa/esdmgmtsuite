---
trigger: always_on
description: "Regla de preferencia global de unidad de temperatura (Celsius / Fahrenheit) y conversión automática"
---

# Regla de Unidades de Temperatura

1. **Preferencia en Ajustes:** El sistema debe ofrecer en el menú de Ajustes (directamente debajo del selector de idioma) la opción de seleccionar la unidad preferida de temperatura: Celsius (°C) o Fahrenheit (°F).
2. **Entrada Numérica Limpia:** Los formularios de captura de condiciones ambientales deben indicar claramente la unidad activa (°C o °F) en la etiqueta y esperar valores numéricos limpios sin símbolos tipados por el usuario.
3. **Conversión y Representación Dinámica:** El sistema debe convertir y presentar dinámicamente las temperaturas históricas según la preferencia activa del usuario:
   - \(°F = (°C \times \frac{9}{5}) + 32\)
   - \(°C = (°F - 32) \times \frac{5}{9}\)
4. **Persistencia Consistente:** Las lecturas se almacenan en base de datos de manera estandarizada y canónica para garantizar la compatibilidad universal entre usuarios y plantas.
