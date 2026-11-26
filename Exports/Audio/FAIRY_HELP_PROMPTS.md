# The Fairy's calls for help: ElevenLabs prompts (2026-11-26)

**WIRED 2026-11-27** (user "audio added"; Wire ElevenLabs Sounds (P1)): her cage plays calls 1 and 2, the jar calls 3
and 2 (3 and 4 once `fairy_help_call_4` is in). **RAISED** (user "Yes, raise them"): call 1 +12 dB, call 2 +6 dB into
a limiter (peaks -1.8 dB), 160 kbps mp3; loudest moments now -9.5 / -8.0 LUFS (were -14.2 / -11.1; the old cage calls
-3.9 / -8.3: call 1 can't get nearer without squashing). Originals: Documents/429R_claude_backups/2026-11-26/
fairy_calls_original/.

User: "we need new calling for help sound for fairy". Her help calls now play in two places:
- in her cage at the start ("Move to Fairy", you walk toward her voice): `Fairy_Call_1`, `Fairy_Call_2` (Fairy prefab);
- in the jar at the boss: `FairyCallHelp`, `FairyHelp` (Story_Fairy_Jar).

The 4 new lines below replace them. (The "please find me" when you take a seed piece stays as it is.)

**Voice:** the same Fairy voice as in `P1_PROMPTS.md` (Voice Design: "An adult woman voicing a tiny cartoon fairy in a
fantasy video game. High-pitched, light and bright, sweet and bubbly, warm and kind ... Studio quality, close to the
microphone, no reverb."). Then Text to Speech with that voice, model Eleven v3, Stability "Creative"; the [brackets]
are delivery tags. Make 3-4 takes of each line and keep the best.

The calls are 3D sounds you walk toward in the dark, so a clear, projected call works better than a whisper.

| File (new) | Line | About | Plays |
|---|---|---|---|
| `fairy_help_call_1` | [calling out from far away, scared] Help! Somebody, help me! | 2 s | her cage at the start |
| `fairy_help_call_2` | [calling out, desperate] Over here! Please, help! | 2 s | her cage at the start |
| `fairy_help_call_3` | [crying out, trapped] Help me! I'm trapped in here! | 2 s | the jar at the boss |
| `fairy_help_call_4` | [weak, pleading] Please... get me out of here... | 2.5 s | the jar at the boss |

Save them in `Assets/Audio/SFX/` with these names (.mp3 is fine) and say "the sounds are in": I'll put 1-2 on the
cage's calls and 3-4 on the jar. The old clips stay in the project, unused.

ElevenLabs blocks Voice Design prompts that sound like a child ("young girl", "kid") or "breathy": always describe an
adult voice actor.
