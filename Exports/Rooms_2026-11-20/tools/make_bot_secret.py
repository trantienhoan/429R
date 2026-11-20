# Builds bot_secret.cs (2026-11-20) from bot_rooms.cs: a normal-player run to the bedroom that photographs it, then tests
# the secret room (secret_block.cs.txt), the W/C door's fall, and the fold at the bedroom fight; ST_SPIDER picks the outcome.
s = open(r"K:/WORK_2026/08_04/_claude/rooms_2026-11-20/bot_rooms.cs", encoding="utf-8").read()
block = open(r"K:/WORK_2026/08_04/_claude/rooms_2026-11-20/secret_block.cs.txt", encoding="utf-8").read()
def rep(a, b):
    global s
    assert s.count(a) == 1, a[:80]
    s = s.replace(a, b)
rep('string lastPhotoRoom = VARIANT == "pumpkin" ? "2_bedroom" : "4_playroom";', 'string lastPhotoRoom = "none";')
rep('System.Action tick = null;', block + '\nSystem.Action tick = null;')
rep('            takenRooms.Add(photoJob[0]);\n', '            takenRooms.Add(photoJob[0]);\n            if (photoJob[0] == "2_bedroom" && st == 0) { st = 1; stAt = Time.realtimeSinceStartup; Log("secret test starts"); }\n')
rep('''            photoJob = null;
        }
''', '''            photoJob = null;
        }
        if (st > 0 && st != 10) { RunSecret(state); return; }
        if (st == 10 && state == "Enemy1 - Wait" && inState > 5f)
        {
            Check("bedroom fight: secret room folded away (" + SecretWallsShown() + " renderers shown)", !kitR.IsUp("Secret") && SecretWallsShown() == 0);
            var lamp = secret.transform.Find("Secret Room Light");
            Check("secret light off", lamp == null || !lamp.gameObject.activeInHierarchy);
            var fl = secret.transform.Find("Secret_Floor"); int flOn = 0; if (fl != null) foreach (var r in fl.GetComponentsInChildren<Renderer>()) if (r.enabled) flOn++;
            Check("secret floor broken away (" + flOn + " tiles left)", flOn == 0);
            var tpGone = secret.transform.Find("Secret Teleport Area");
            Check("secret teleport area off", tpGone == null || !tpGone.gameObject.activeInHierarchy);
            Check("fall edge back over the secret room's spot (0, 8)", Game.Locomotion.FallEdge.Covers(new Vector3(0f, 0.1f, 8f)) && Game.Locomotion.FallEdge.Covers(new Vector3(0f, 0.1f, 4.7f)));
            Check("bedroom floor edge as before (no fall at 0, 4.3)", !Game.Locomotion.FallEdge.Covers(new Vector3(0f, 0.1f, 4.3f)));
            try { ShotAt("6_" + tagV + "_4_fight_folded_aerial", new Vector3(10f, 13f, -7f), new Vector3(0f, 0f, 4f), 50f, true); } catch (System.Exception pe) { Log("PHOTO ERROR " + pe.Message); }
            Log("SECRET SUMMARY: " + string.Join(" | ", stResults.ToArray()));
            Log("PHOTOS DONE secret");
            UnityEngine.Object.Destroy(marker);
            return;
        }
''')
open(r"K:/WORK_2026/08_04/_claude/rooms_2026-11-20/bot_secret.cs", "w", encoding="utf-8").write(s)
print("ok", len(s))
