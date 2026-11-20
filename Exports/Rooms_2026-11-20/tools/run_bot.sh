#!/bin/sh
# sh run_bot.sh <stuffs|chaos|pumpkin>: fresh new game on the bot profile, the room-photo bot plays like a player until the
# last room is photographed (PHOTOS DONE), then Play stops. Log: bot_<variant>_log.txt; pictures: walls/.
D=K:/WORK_2026/08_04/_claude/rooms_2026-11-20
P=K:/WORK_2026/08_04/_claude/playtest_2026-11-10
V=$1
sh $P/pt.sh $P/pt_state.cs 30 | grep -o '"result":"[^"]*' | cut -c1-90
sh $P/pt.sh $P/pt_start.cs 60 | grep -o '"result":"[^"]*' | cut -c1-120
for i in $(seq 1 30); do
  timeout 4 tail -f /dev/null
  out=$(sh $P/pt.sh $P/pt_start.cs 60 | grep -o '"result":"[^"]*')
  case "$out" in *"RUN STARTED"*) echo "$out" | cut -c1-120; break;; esac
done
for i in 1 2; do timeout 4 tail -f /dev/null; done
printf 'UnityEditor.SessionState.SetString("RT_VARIANT", "%s"); return "RT_VARIANT = %s";\n' "$V" "$V" > $D/cfg_bot.cs
sh $P/pt.sh $D/cfg_bot.cs 30 | grep -o '"result":"[^"]*'
rm -f $D/bot_${V}_log.txt
sh $P/pt.sh $D/bot_rooms.cs 60 | grep -o '"result":"[^"]*'
for i in $(seq 1 240); do
  timeout 5 tail -f /dev/null
  if grep -q "PHOTOS DONE\|bot stopped" $D/bot_${V}_log.txt 2>/dev/null; then break; fi
done
grep -E "^\[.*\] (->|PHOTO|swapped|pulled|STALL|BOT ERROR|PHOTOS DONE|bot stopped)" $D/bot_${V}_log.txt | cut -c1-200
sh $P/pt.sh $P/pt_stop.cs 60 | grep -o '"result":"[^"]*' | cut -c1-120
