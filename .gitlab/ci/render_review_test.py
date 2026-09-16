"""Proves render_review can go red. Run: python .gitlab/ci/render_review_test.py"""
import json
import os
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPT = os.path.join(HERE, "render_review.py")


def run(result_text):
    with tempfile.TemporaryDirectory() as tmp:
        env_path = os.path.join(tmp, "envelope.json")
        out_path = os.path.join(tmp, "note.json")
        with open(env_path, "w", encoding="utf-8") as fh:
            json.dump({"result": result_text}, fh)
        proc = subprocess.run(
            [sys.executable, SCRIPT, env_path, out_path, "abc123def456"],
            capture_output=True, text=True,
        )
        body = None
        if os.path.exists(out_path):
            with open(out_path, encoding="utf-8") as fh:
                body = json.load(fh)["body"]
        return proc.returncode, proc.stdout.strip(), proc.stderr.strip(), body


failures = []


def check(name, ok, detail=""):
    print(("PASS  " if ok else "FAIL  ") + name + ("" if ok else f"  <- {detail}"))
    if not ok:
        failures.append(name)


# GREEN: a real-shaped verdict with a blocking and a non-blocking finding
code, out, err, body = run(json.dumps({
    "verdict": "changes_requested",
    "summary": "One unauthenticated path to clinical text.",
    "findings": [
        {"file": "README.md", "blocking": False, "title": "Stale command", "detail": "Says tests/Unit."},
        {"file": "src/Api/Program.cs", "line": 410, "blocking": True, "title": "Health lies",
         "detail": "Returns 200 while the provider is unreachable."},
    ],
}))
check("mixed findings render", code == 0 and "BLOCKING=1 TOTAL=2" in out, err or out)
check("blocking finding sorts first", body and body.index("Health lies") < body.index("Stale command"))
check("line number is shown", body and "`src/Api/Program.cs:410`" in body)
check("head sha is named", body and "abc123def456" in body)

# GREEN: clean approval, no findings
code, out, err, body = run(json.dumps({"verdict": "approve", "summary": "Clean.", "findings": []}))
check("clean approval", code == 0 and "BLOCKING=0 TOTAL=0" in out, err or out)
check("says No findings", body and "No findings." in body)

# GREEN: a fenced answer is still a cooperating model
code, out, err, _ = run('```json\n{"verdict":"approve","summary":"ok","findings":[]}\n```')
check("tolerates a code fence", code == 0, err or out)

# RED: the two halves of the verdict disagree
code, out, err, _ = run(json.dumps({
    "verdict": "approve", "summary": "ok",
    "findings": [{"file": "a", "blocking": True, "title": "t", "detail": "d"}],
}))
check("rejects approve-with-blocking", code == 2 and "blocking" in err, err)

# RED: not the agreed shape
code, out, err, _ = run('{"verdict":"approve"}')
check("rejects missing findings", code == 2, err)

# RED: not JSON at all
code, out, err, _ = run("I reviewed it and it looks fine to me!")
check("rejects prose", code == 2, err)

print()
print("FAILED" if failures else "all green")
sys.exit(1 if failures else 0)
