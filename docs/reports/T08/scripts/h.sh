export PATH="/c/Users/2273129/AppData/Local/Microsoft/dotnet:$PATH" DOTNET_ROOT='C:\Users\2273129\AppData\Local\Microsoft\dotnet'
W='/c/Users/2273129/AppData/Local/Temp/claude/c--Users-2273129-Documents-Projects-TemplateGenerator/318d2cf1-dde3-4bfe-96a2-838beef3a510/scratchpad/t08-manual'
LOGS="$W/logs"
# run <logname> <command...>: runs literally via eval, logs cmd/output/exit
run(){ local name=$1; shift; local f="$LOGS/$C-$name.log"; printf '$ %s\n' "$*" > "$f"; ( eval "$*" ) >> "$f" 2>&1; local ec=$?; echo "[exit=$ec]" >> "$f"; echo "== $C-$name: \$ $*"; echo "   exit=$ec"; grep -vE "^ *(% Total|Dload|0  ? ? ?0 )" "$f" | tail -n ${TAILN:-6} | sed 's/^/   | /'; return 0; }
snap(){ (cd "$W/$C" && find . -type f -not -path '*/bin/*' -not -path '*/obj/*' -not -name '*.db' -not -name '*.db-*' | sort | xargs -d '\n' sha256sum) > "$LOGS/$C-src.$1"; }
# start app in background: startapp <logname> <command>
startapp(){ local name=$1; shift; local f="$LOGS/$C-$name.log"; printf '$ %s\n' "$*" > "$f"; ( eval "$*" ) >> "$f" 2>&1 < /dev/null & 
  for i in $(seq 1 120); do s=$(curl -s -o /dev/null -w '%{http_code}' http://localhost:5100/health 2>/dev/null); [ "$s" = 200 ] && break; sleep 1; done; echo "== $C-$name started: health=$s after ${i}s"; grep -E 'Now listening|Hosting environment|Content root' "$f" | sed 's/^/   | /'; }
stopapp(){ local pid; pid=$(netstat -ano | grep ':5100 ' | grep LISTENING | awk '{print $5}' | sort -u | head -1); if [ -n "$pid" ]; then tasklist //FI "PID eq $pid" //NH; taskkill //PID $pid //T //F; fi; sleep 2; netstat -ano | grep ':5100 ' | grep LISTENING || echo "   port 5100 free"; }
code(){ curl -s -o /dev/null -w '%{http_code}' "$@"; }
