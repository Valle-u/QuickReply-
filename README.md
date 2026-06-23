# QuickReply

Programa de escritorio (Windows) para pegar textos predeterminados con una tecla/combinación.
Reemplazo cómodo de AutoHotkey para atención al cliente (kommo, livechat).

Hecho en **C# / .NET 10 (WPF)**. Corre en la bandeja del sistema, sin instalación ni permisos de administrador.

---

## Estado: Fase 1 (motor base) ✅

Lo que ya funciona:

- Corre de fondo con ícono en la bandeja del sistema.
- Lee los textos desde `snippets.json` (se crea solo la primera vez con ejemplos).
- Engancha el teclado a bajo nivel y dispara por **combinaciones** (ej: `Ctrl+Shift+S`) y **teclas dedicadas** (ej: `F2`).
- Pega por **portapapeles** (Ctrl+V): soporta saltos de línea, acentos y emojis sin romperse.
- Guarda y **restaura tu portapapeles** original después de pegar.
- **Auto-enviar** opcional por texto (manda Enter después de pegar).
- Menú de bandeja: Editar textos · Recargar · Pausar/Reanudar · Salir.

## Cómo usar

1. Ejecutá `QuickReply.exe`.
2. Doble clic en el ícono de la bandeja (o clic derecho → **Editar textos…**) para abrir `snippets.json`.
3. Editá tus textos y guardá el archivo.
4. Clic derecho en el ícono → **Recargar** (o reiniciá la app).
5. En cualquier chat, apretá el disparador y se pega el texto.

> **Pausar/Reanudar**: útil si querés escribir literalmente una abreviatura sin que se dispare.

## Formato de `snippets.json`

```json
{
  "Settings": {
    "PasteDelayMs": 60,
    "EnterDelayMs": 90,
    "RestoreClipboardDelayMs": 140
  },
  "Snippets": [
    {
      "Title": "Saludo inicial",
      "Category": "General",
      "Trigger": "Ctrl+Shift+S",
      "AutoSend": false,
      "Content": "¡Hola! 👋 ¿En qué puedo ayudarte hoy?"
    }
  ]
}
```

- **Trigger**: `Ctrl`, `Alt`, `Shift`, `Win` + una tecla. Ejemplos: `F2`, `Ctrl+Shift+S`, `Alt+1`, `Win+Space`.
- **AutoSend**: `true` para pegar y mandar Enter automáticamente.
- **Content**: podés usar saltos de línea (`\n` en el JSON, o líneas reales) y emojis.

### Notas

- Los emojis en el archivo de ejemplo aparecen como códigos `\uXXXX`, pero se pegan perfecto.
  Si los escribís literales (👋) también funciona y se respeta.
- Evitá usar como tecla dedicada una tecla de escritura normal (una letra suelta), porque se "tragaría"
  esa tecla globalmente. Usá F1–F12, numpad o combinaciones con Ctrl/Alt/Shift.
- En teclados latinoamericanos, **AltGr = Ctrl+Alt**. Si usás combinaciones `Ctrl+Alt+...` pueden chocar
  con caracteres como `@` o `{}`. Recomendado: `Ctrl+Shift+...` o teclas de función.

## Compilar / ejecutar

```powershell
dotnet build -c Debug          # compilar
dotnet run                     # compilar y ejecutar
dotnet publish -c Release      # build de Release
```

---

## Roadmap

- **Fase 1 — Motor base** ✅
- **Fase 2 — Interfaz de configuración** (lista + categorías + buscador, editor, captura de tecla "apretá y aplicá", detección de conflictos, exportar/importar)
- **Fase 3 — Disparadores avanzados** (abreviaturas que se expanden + ventana de búsqueda emergente)
- **Fase 4 — Texto dinámico** (`{cursor}`, `{fecha}`/`{hora}`, variables como `{nombre}`)
- **Fase 5 — Pulido** (iniciar con Windows, minimizar a bandeja, hotkey de pausa, backups, ícono propio)
