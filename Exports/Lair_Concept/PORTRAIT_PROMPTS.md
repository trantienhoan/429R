# The evil pumpkin's portrait, growing room by room (2026-11-26)

User picked "Portrait grows room by room": in every lair the same framed portrait hangs, and from room to room the
evil pumpkin in it gets bigger and fatter while the Fairy's jar light gets weaker. It tells the WHY without words: he
keeps her power and grows on it.

Room 1 (storage room) is done: `Assets/M_Textures/Lair/Lair_Portrait_Room1.png` (512 x 512).

## How to make rooms 2-4 (Gemini)

1. Open a new Gemini chat, attach `Assets/M_Textures/Lair/Lair_Portrait_Room1.png`.
2. Paste the room 2 prompt. Keep the picture you like, then in the SAME chat paste room 3, then room 4 (each builds on
   the last one, so the pumpkin keeps the same face).
3. Save them square (1:1, 1024 x 1024 is fine) as `Lair_Portrait_Room2.png`, `Lair_Portrait_Room3.png`,
   `Lair_Portrait_Room4.png` into `Assets/M_Textures/Lair/`, then tell Claude "the portraits are in".

```text
Room 2 (bedroom):
Repaint this exact portrait, same style, same framing, square, no frame, no text: the same sly evil jack-o'-lantern
in his dark little coat, now clearly bigger and fatter, his coat stretched tight, his grin wider and greedier. The
glass jar in his hand looks smaller next to him and the tiny green fairy inside glows weaker. Same dark background,
the green glow from the jar dimmer.
```

```text
Room 3 (living room):
The same portrait again: the pumpkin is now huge and fills most of the frame, thin cracks of green light glow in his
rind as if he soaked up her light, his eyes glow green-yellow. The jar is tiny in his fingers and the fairy's light
is almost out.
```

```text
Room 4 (playroom):
The same portrait one last time: the pumpkin is so big he bursts past the edges of the painting, his grin and green
glowing cracks filling the picture, vines curling out from behind him. The jar is a small dark speck in his hand with
only a faint green spark left.
```

When they are in, Claude hangs them: each lair room gets its own copy of the gilded frame with its painting (re-runnable
Set Up Lair Portrait).
