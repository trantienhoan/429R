#!/bin/sh
# usage: batch_close.sh tag "KEY=v KEY=v" views samples
cd /c/Github_Repository/429R/Exports/Steam_Art
"E:/Steam/steamapps/common/Blender/blender.exe" -b variants/knight_dev.blend --python assets/knight/kp_render.py -- tag=$1 pass views=$3 samples=$4 $2 2>&1 | grep -E "RENDERED|Error|Trace|pos mixamorig:RightHand"
