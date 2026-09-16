#!/usr/bin/env sh
# Fails if the OpenEMR fork submodule and the published-image pins disagree.
#
# The submodule (external/agent-forge) records WHICH SOURCE the deployed OpenEMR image was built
# from; docker-compose.yml and .railway/railway.ts record WHICH IMAGE gets deployed. They are two
# halves of one fact, in three files, so they drift silently - the pin was already three commits
# behind the fork when the submodule was introduced. This turns that into a loud failure.
#
# Run it locally before opening an MR; CI runs it too (.github/workflows/ci.yml).
# reference: documentation/DEPLOYMENT.md section 1
set -eu

fail() { echo "verify-openemr-pin: $1" >&2; exit 1; }

# The gitlink from the INDEX, not the checked-out submodule contents: this works without
# `submodule update --init`, and pre-commit it reflects what is about to be committed rather
# than what HEAD still says.
gitlink=$(git ls-files -s external/agent-forge | awk '{print $2}')
[ -n "$gitlink" ] || fail "no submodule gitlink at external/agent-forge"
short=$(printf '%s' "$gitlink" | cut -c1-12)

compose_tag=$(sed -n 's/.*ghcr\.io\/adammarquette\/agent-forge:sha-\([0-9a-f]\{12\}\).*/\1/p' docker-compose.yml | head -1)
[ -n "$compose_tag" ] || fail "no sha-<12> OpenEMR pin found in docker-compose.yml"

railway_tag=$(sed -n 's/.*ghcr\.io\/adammarquette\/agent-forge:sha-\([0-9a-f]\{12\}\).*/\1/p' .railway/railway.ts | head -1)
[ -n "$railway_tag" ] || fail "no sha-<12> OpenEMR pin found in .railway/railway.ts"

status=0
if [ "$compose_tag" != "$short" ]; then
    echo "MISMATCH docker-compose.yml   pins sha-$compose_tag  but the submodule is at $short" >&2
    status=1
fi
if [ "$railway_tag" != "$short" ]; then
    echo "MISMATCH .railway/railway.ts  pins sha-$railway_tag  but the submodule is at $short" >&2
    status=1
fi

if [ "$status" -ne 0 ]; then
    cat >&2 <<'MSG'

Fix by moving BOTH halves in one commit:
  git -C external/agent-forge fetch origin
  git -C external/agent-forge checkout <fork-commit>   # the commit the new image was built from
  git add external/agent-forge
  # then set ghcr.io/adammarquette/agent-forge:sha-<first-12> in docker-compose.yml
  # and .railway/railway.ts
MSG
    exit 1
fi

echo "verify-openemr-pin: OK - submodule $short matches both image pins"
