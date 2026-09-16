"""Turns the reviewer's JSON verdict into a GitLab note payload.

Kept out of review.sh because it is the part with branches worth testing: the shape check, the
"no findings" case, and the markdown. Run render_review_test.py against it.

  python render_review.py <cli-envelope.json> <note-payload-out.json> <head-sha>

Prints `VERDICT=<v> BLOCKING=<n> TOTAL=<n>` on success; exits non-zero with a message on a
malformed verdict, because a verdict nobody can parse must fail the job rather than pass an
unreviewed merge request.
"""
import json
import sys

FOOTER = (
    "_Posted by the Code Reviewer job (`.gitlab/ci/review.sh`). Contract: "
    "`documentation/agents/code-reviewer.md`. This job never edits code and cannot approve an MR._"
)


def extract(envelope_text):
    """The CLI wraps the agent's answer in an envelope; the answer itself is the JSON we want."""
    envelope = json.loads(envelope_text)
    result = envelope.get("result")
    if not result:
        raise ValueError("no `result` in the CLI envelope")
    # A model that fences its JSON is still a cooperating model; anything else is not.
    text = result.strip()
    if text.startswith("```"):
        text = text.split("\n", 1)[1] if "\n" in text else text
        text = text.rsplit("```", 1)[0]
    return json.loads(text)


def validate(verdict):
    if verdict.get("verdict") not in ("approve", "changes_requested"):
        raise ValueError("verdict must be 'approve' or 'changes_requested'")
    if not isinstance(verdict.get("findings"), list):
        raise ValueError("findings must be a list")
    blocking = [f for f in verdict["findings"] if f.get("blocking")]
    # The two halves of the verdict must agree, or the job status and the note say different things.
    if blocking and verdict["verdict"] != "changes_requested":
        raise ValueError("blocking findings present but verdict is 'approve'")
    return blocking


def render(verdict, head_sha):
    blocking = [f for f in verdict["findings"] if f.get("blocking")]
    headline = (
        "**Code Reviewer: no blocking findings.**"
        if verdict["verdict"] == "approve"
        else "**Code Reviewer: changes requested.**"
    )
    lines = [
        f"{headline} {verdict.get('summary', '')}".rstrip(),
        "",
        f"Reviewed `{head_sha}`. An approval is a claim about this revision — a push after this "
        "comment is unreviewed again.",
        "",
    ]
    if not verdict["findings"]:
        lines.append("No findings.")
    else:
        # Blocking first: the reader who stops after one bullet should have read the worst one.
        for f in blocking + [f for f in verdict["findings"] if not f.get("blocking")]:
            where = f.get("file", "(unknown)")
            if f.get("line"):
                where += f":{f['line']}"
            mark = "**blocking** " if f.get("blocking") else ""
            lines.append(f"- {mark}`{where}` — **{f.get('title', 'finding')}**  ")
            lines.append(f"  {f.get('detail', '').strip()}")
    lines += ["", "---", FOOTER, "", "Assisted-by: Claude Opus 5 (Claude Code)"]
    return "\n".join(lines)


def main(argv):
    envelope_path, out_path, head_sha = argv[1], argv[2], argv[3]
    with open(envelope_path, encoding="utf-8") as fh:
        verdict = extract(fh.read())
    blocking = validate(verdict)
    with open(out_path, "w", encoding="utf-8") as fh:
        json.dump({"body": render(verdict, head_sha)}, fh)
    print(f"VERDICT={verdict['verdict']} BLOCKING={len(blocking)} TOTAL={len(verdict['findings'])}")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main(sys.argv))
    except (ValueError, KeyError, IndexError, json.JSONDecodeError) as exc:
        print(f"render_review: {exc}", file=sys.stderr)
        sys.exit(2)
