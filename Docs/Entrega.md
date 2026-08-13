# Entrega del proyecto

## Lo primero: "deploy" no es mandar la base de datos

La base de datos **no viaja con el proyecto**. Está en Supabase, que es un
servicio en la nube: vive en `https://gavlmybnbfwlvgtoeijy.supabase.co` y
está funcionando ahora mismo. Cualquiera que abra el juego —en su PC, en
otro país, desde el navegador— habla con esa misma base por internet. No
hay nada que instalar ni que copiar para que el ranking funcione.

Dicho de otro modo: **la base de datos ya está desplegada**. Lo único que
falta por desplegar es el juego.

Eso deja tres entregas posibles, según lo que os hayan pedido:

| Qué piden | Qué se entrega |
|---|---|
| "El proyecto" | El repositorio de GitHub, o un ZIP de la carpeta |
| "El juego para probarlo" | El ejecutable de Windows, o el enlace web |
| "El deploy" | El juego publicado en una URL que se abre y se juega |

## 1. El juego publicado (esto es el deploy)

Compilar para web, desde el editor con **TetrisTakana → Compilar para web**,
o sin abrirlo:

```
"C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe" -batchmode -quit -nographics -projectPath . -buildTarget WebGL -executeMethod TetrisTakana.EditorTools.WebBuilder.BuildWeb
```

Queda en `Build/Web`, con `index.html`, `Build/` y `TemplateData/`. Esa
carpeta se sube tal cual a cualquier hosting estático:

- **itch.io** — subir la carpeta comprimida, marcar "This file will be
  played in the browser". Es lo que se suele pedir en un trabajo de clase.
- **Netlify** — arrastrar la carpeta a netlify.com/drop.
- **GitHub Pages** — subir el contenido a la rama `gh-pages`.

Los tres sirven la web y el juego llama a Supabase desde el navegador, así
que el ranking funciona igual que en el ejecutable.

**Antes de publicar en abierto**, dos cosas que conviene mirar:

- El build pesa ~77 MB, y el 83% son texturas. Hay ocho PNG de ~6 MB cada
  uno sin comprimir (`Bomba.png`, `PixelSinFondo.png`, los paneles de
  pausa y puntuaciones…). Ponerles compresión en el importador baja el
  peso mucho. Otros 6,5 MB son del paquete `com.unity.ai.inference`, que
  el juego no usa y se puede quitar de `Packages/manifest.json`.
- Hay ocho efectos de sonido rippeados del Tetris de Game Boy en
  `Assets/Audio/Sound Effects/`. Para entregar al profesor da igual; para
  dejarlo colgado en internet con enlace público, habría que
  sustituirlos.

## 2. El proyecto

El repositorio es `github.com/betzabxscobar/TetrisTakana`. Si hay que
mandar un ZIP en vez del enlace, **no incluir** estas carpetas, que se
regeneran solas y pesan muchísimo:

```
Library/  Temp/  Obj/  Build/  Builds/  Logs/  UserSettings/
```

Son justo las que ya están en el `.gitignore`.

## 3. La base de datos

Para que otra persona pueda **recrearla** (montar su propio Supabase y
apuntar el juego ahí), está [BaseDeDatos.sql](BaseDeDatos.sql): dos tablas
(`players`, `game_sessions`), dos funciones (`ensure_player`,
`leaderboard`) y las políticas de seguridad.

Ese archivo está **reconstruido a partir del cliente del juego**, no
volcado del proyecto real. Si hace falta el volcado de verdad —con los
datos y con cualquier cambio hecho a mano desde el panel— se saca así:

```
supabase link --project-ref gavlmybnbfwlvgtoeijy
supabase db dump -f Docs/BaseDeDatos.sql
supabase db dump -f Docs/Datos.sql --data-only
```

### Cómo apuntar el juego a otra base

En `Assets/Resources/SupabaseConfig.asset` están la URL y la clave. Se
cambian desde el inspector, sin tocar código.

La clave que hay ahí es la **publishable**, que es la correcta: está hecha
para viajar dentro del juego y que la lea cualquiera. Lo que protege los
datos son las políticas RLS del `.sql`, no la clave. **Nunca poner ahí la
`service_role` ni la `sb_secret_`**: saltan la seguridad entera y el juego
funcionaría igual de bien mientras reparte esa llave por el mundo. El
propio asset avisa por consola si detecta que se ha puesto la que no es.

## Cómo funciona el ranking, en corto

1. El juego entra con un **usuario anónimo** de Supabase Auth: sin correo
   ni contraseña. El token de refresco se guarda en `PlayerPrefs`, así que
   el mismo jugador conserva su identidad entre partidas.
2. La primera vez que una partida entra en el ranking, se le pide el
   nombre y se llama a `ensure_player`.
3. Al terminar cada partida se inserta una fila en `game_sessions`.
4. La pantalla de puntuaciones llama a `leaderboard`, que devuelve la
   mejor partida de cada jugador en ese modo, ordenadas.
