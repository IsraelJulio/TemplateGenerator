. '/c/Users/2273129/AppData/Local/Temp/claude/c--Users-2273129-Documents-Projects-TemplateGenerator/318d2cf1-dde3-4bfe-96a2-838beef3a510/scratchpad/t08-manual/scripts/h.sh'
W="$W/rerun"; LOGS="$W/logs"
# st <log>: first HTTP status line(s) and exit
st(){ echo "   -> $(grep -E 'HTTP/1.1|curl: \(' "$LOGS/$C-$1.log" | tr -d '\r' | paste -sd' ') $(tail -1 "$LOGS/$C-$1.log")"; }
