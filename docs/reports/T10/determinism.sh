#!/usr/bin/env bash
# T10 - determinismo por SHA-256 e rejeicoes, contra a API real (POST /api/templates).
# Uso: bash determinism.sh <dir-de-saida>   (a API deve estar em http://localhost:5099)
OUT="$1"
API=http://localhost:5099/api/templates
set -x
for c in "simple none none true" "clean postgresql identity false" "clean sqlite jwt true" "simple sqlite identity true"; do
  set -- $c
  body="{\"projectName\":\"Acme.Billing.Api\",\"architecture\":\"$1\",\"database\":\"$2\",\"authentication\":\"$3\",\"swagger\":$4,\"dotnetVersion\":\"net10.0\"}"
  curl -sS -o "$OUT/z1.zip" -w 'HTTP %{http_code}\n' -X POST -H 'Content-Type: application/json' -d "$body" "$API"; echo "EXIT=$?"
  curl -sS -o "$OUT/z2.zip" -w 'HTTP %{http_code}\n' -X POST -H 'Content-Type: application/json' -d "$body" "$API"; echo "EXIT=$?"
  sha256sum "$OUT/z1.zip" "$OUT/z2.zip" | cut -c1-64; echo "EXIT=$?"
  cmp -s "$OUT/z1.zip" "$OUT/z2.zip"; echo "IDENTICOS_EXIT=$?"
done
curl -sS -w '\nHTTP %{http_code}\n' -X POST -H 'Content-Type: application/json' -d '{"projectName":"1-invalido","architecture":"simple","database":"none","authentication":"none","swagger":true,"dotnetVersion":"net10.0"}' "$API"; echo "EXIT=$?"
curl -sS -w '\nHTTP %{http_code}\n' -X POST -H 'Content-Type: application/json' -d '{"projectName":"Acme.Api","architecture":"simple","database":"none","authentication":"identity","swagger":true,"dotnetVersion":"net10.0"}' "$API"; echo "EXIT=$?"
