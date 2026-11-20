# Builds bot_rooms.cs (2026-11-20) from fixlist_2026-11-11/test2/bot_run.cs: a normal-player run that stops to photograph
# each room's 4 walls + plan once its furniture is in, and steers each magic table to the wanted variant (RT_VARIANT =
# stuffs | chaos | pumpkin) by swapping a wrong skull for the wanted one where it popped out.
s = open(r"K:/WORK_2026/08_04/_claude/fixlist_2026-11-11/test2/bot_run.cs", encoding="utf-8").read()
photo = open(r"K:/WORK_2026/08_04/_claude/rooms_2026-11-20/photo_block.cs.txt", encoding="utf-8").read()


def rep(a, b, cnt=1):
    global s
    assert s.count(a) == cnt, (s.count(a), a[:70])
    s = s.replace(a, b)


rep(r'var logPath = @"K:\WORK_2026\08_04\_claude\fixlist_2026-11-11\test2\bot_bot_log.txt";',
    r'var logPath = @"K:\WORK_2026\08_04\_claude\rooms_2026-11-20\bot_" + UnityEditor.SessionState.GetString("RT_VARIANT", "stuffs") + "_log.txt";')
rep('var consolePath = "K:/WORK_2026/08_04/_claude/fixlist_2026-11-11/test/bot_console.txt";',
    'var consolePath = "K:/WORK_2026/08_04/_claude/rooms_2026-11-20/bot_console.txt";')
# don't restart a slot the run already started
rep('if (menu != null) { menu.StartSlot(0);', 'if (menu != null && Game.Saving.SaveManager.CurrentSlot <= 0) { menu.StartSlot(0);')
# the photo helpers go before the tick
rep('System.Action tick = null;', photo + '\nSystem.Action tick = null;')
# on entering a furnish state: arm the photos (5 s for the sets and the pumpkin rain)
rep('            visited.Add(state); last = state; since = Time.time; stallLogged = false; hatPulled = false;',
    '''            visited.Add(state); last = state; since = Time.time; stallLogged = false; hatPulled = false; skullSwapped = false;
            string[] pj;
            if (photoStates.TryGetValue(state, out pj) && !takenRooms.Contains(pj[0])) { photoJob = pj; photoAt = Time.realtimeSinceStartup + 5f; Log("photos of " + pj[0] + " " + pj[3] + " in 5 s"); }''')
rep('bool hatPulled = false;', 'bool hatPulled = false; bool skullSwapped = false;')
# hold still while photos are pending; take them; stop after the last room
rep('        if (Time.timeScale == 0f) return;\n',
    '''        if (Time.timeScale == 0f) return;
        if (photoJob != null)
        {
            if (Time.realtimeSinceStartup < photoAt) return;
            try { TakeRoom(photoJob); } catch (System.Exception pe) { Log("PHOTO ERROR " + pe); }
            takenRooms.Add(photoJob[0]);
            if (photoJob[0] == lastPhotoRoom) { Log("PHOTOS DONE " + VARIANT); photoJob = null; UnityEngine.Object.Destroy(marker); return; }
            photoJob = null;
        }
''')
# magic tables: the wanted skull; a wrong one is swapped for it where it popped out
rep('''            var skull = Global(skullName);
            if (!(skull && skull.activeInHierarchy)) skull = Global(skullName == "Skull_Chaos" ? "Skull_Stuffs" : "Skull_Chaos");
            if (!(skull && skull.activeInHierarchy)) skull = Global("Skull_Pumpkin");
            if (skull && skull.activeInHierarchy) Touch(skull);''',
    '''            string want = (state == "Magic_Table" || state == "Pumpkin_7") && wantSkull == "Skull_Pumpkin" ? "Skull_Stuffs" : wantSkull;
            var skull = Global(want);
            GameObject wrong = null;
            foreach (var sn in new[] { "Skull_Chaos", "Skull_Stuffs", "Skull_Pumpkin" }) { var g = Global(sn); if (sn != want && g && g.activeInHierarchy) wrong = g; }
            if (skull && skull.activeInHierarchy && skullSwapped) Touch(skull);
            else if (skull && skull.activeInHierarchy && wrong == null) Touch(skull);
            else if (wrong != null && !skullSwapped)
            {
                var pf = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Items/Breakable/" + want + ".prefab");
                var at = wrong.transform.position; var par = wrong.transform.parent;
                UnityEngine.Object.Destroy(wrong);
                UnityEngine.Object.Instantiate(pf, at, Quaternion.identity, par);
                skullSwapped = true; Log("swapped " + Base(wrong.name) + " for " + want + " (variant " + VARIANT + ")");
            }''')
open(r"K:/WORK_2026/08_04/_claude/rooms_2026-11-20/bot_rooms.cs", "w", encoding="utf-8").write(s)
print("ok", len(s))
