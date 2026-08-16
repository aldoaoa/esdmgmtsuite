---
trigger: always_on
description: "Regla obligatoria de versionado continuo del sistema esd360 y notificación en respuestas"
---

# Regla de Versionado del Sistema esd360

## 1. Invariantes de Código (Actualización de Versión)
En cada iteración, corrección o nueva funcionalidad, se debe actualizar el número de versión (SemVer `vX.Y.Z`) en:
- `ESDSuite.Core/Constants/EsdConstants.cs` -> Constante `SystemVersion`.
- `ESDSuite.Web/Pages/Index.cshtml` -> Badges de versión `#login-version-badge` y `#system-version-badge`.

## 2. Invariante de Respuestas del Asistente
Al concluir cualquier tarea o respuesta dirigida al usuario, se debe incluir explícitamente al final la versión vigente del sistema con el formato:
> **Versión del Sistema:** `vX.Y.Z`

## 3. Invariante de Control de Versiones (Git)
Cada commit que incorpore cambios funcionales o correcciones debe incluir en su mensaje la etiqueta de versión (ej. `chore: bump version to v1.6.2 ...` o `feat(...): ... v1.6.2`).
