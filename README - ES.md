# Filtro de Iconos de BOTC

Aplica un filtro a archivos `.png` similar al usado en los iconos originales de los personajes de *Blood on the Clocktower* por The Pandemonium Institute.  
Por cada archivo procesado, se generan dos versiones: una marcada como **Bueno** (azul) y otra marcada como **Malvado** (rojo), con la etiqueta correspondiente añadida al nombre del archivo.

Hay un archivo `filter.png` en el repositorio. Este es un filtro de ejemplo ( el que yo personalmente uso ) pero puede ser cambiado colocando cualquier otro archivo en su lugar y renombrándolo a `filter.png`.

## Manual de Usuario

### Terminal
> Uso: `<DIRECTORIO DE ENTRADA> <DIRECTORIO DE SALIDA> [númeroAProcesar]`


- `<DIRECTORIO DE ENTRADA>` y `<DIRECTORIO DE SALIDA>` pueden ser rutas de carpeta absolutas o relativas.
- `[númeroAProcesar]` es un entero opcional. Si se omite, se procesarán todos los archivos `.png` del directorio de entrada.
- El directorio de entrada **debe** contener un archivo llamado `filter.png`. Este archivo se utiliza como máscara de ruido y no se procesa él mismo.

### Independiente

El programa pedirá cada valor uno a la vez. Si alguna entrada no es válida, sale sin modificar ningún archivo.
Valores: 
- `INPUT FOLDER` -> Directorio de entrada. Donde el programa busca los archivos. Debe contenter la image llamada `filter.png`
- `OUTPUT FOLDER` -> Directorio de salida. Donde el programa pone los archivos ya procesados. 
- `Images to process` -> Número de imagenes a procesar. El programa procesa el número de imagenes indicadas, empezando por aquellas con cambios más recientes. De no introducir ningún numero, el programa procesa todas las que encuentre.

---

## Advertencia

Forzar el cierre del programa mientras se está ejecutando puede resultar en pérdida de datos.

---


Este proyecto es una herramienta no oficial hecha por fans (por mi) y no está afiliada, respaldada ni patrocinada por The Pandemonium Institute o Steven Medway. Está destinada únicamente para uso de la comunidad.