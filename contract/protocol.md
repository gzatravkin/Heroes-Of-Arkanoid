# Arkanoid WS Protocol (v0)

Transport: WebSocket text frames, JSON. One connection = one game session.

## Client → Server: InputCommand
{ "kind": "PaddleX|Serve|CastImbueIgnite|CastFireball|Cheat", "x": <num>, "cheat": "<op>", "value": <num> }

Cheat ops (dev only): "clearAllButN" (value=N kept), "winNow", "loseNow", "setSeed" (value=seed), "setMana" (value), "addLife"/"loseBall".

## Server → Client: Snapshot (one per tick, ~60/s)
{ tick, phase, lives, spareBalls, mana, manaMax, boardW, boardH, paddleX, paddleW, paddleH, cellSize,
  balls:[{id,x,y,ignited}], blocks:[{id,x,y,hp,maxHp,sprite}], events:[{type,x,y}] }

Coordinates are sim units (origin top-left, +Y down). The renderer scales to canvas.
