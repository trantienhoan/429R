#!/bin/sh
# Usage (Git Bash): sh K:/WORK_2026/08_04/_claude/playtest_2026-11-10/pt.sh <path to .cs> [timeout s]
# Runs a C# 6 method body in the open Unity Editor through the MCP for Unity :8080 fallback (mcp_exec.ps1).
f=$(cygpath -w "$1"); t=${2:-300}
powershell -NoProfile -ExecutionPolicy Bypass -Command "& 'K:\WORK_2026\08_04\_claude\unity_mcp\mcp_exec.ps1' -File '$f' -Timeout $t"
