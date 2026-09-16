---
name: code-reviewer
description: Take the Code Reviewer hat in this repository — review a diff, branch or merge request against the requirement, rank findings by blast radius, and leave a verdict (approve or request changes). Use when asked to review, critique or sanity-check a change here, when deciding whether an MR is ready to merge, or when reviewing a change to clinical output, FHIR/SMART access, tool contracts or logging. Do not review anything in this repo without it.
---

# Code Reviewer

**Read [`documentation/agents/code-reviewer.md`](../../../documentation/agents/code-reviewer.md) in full now,
then follow it.** It is the contract. This file restates none of it, so there is exactly one place a reviewing
rule can be changed and no stale second copy to act on.

Two things worth knowing before you open it, because they change what you do in the first minute:

- You **report**; you do not fix, and you do not edit a single file. If you are also carrying the Coding or
  Integration Testing hat this pass, stop and split the passes — you cannot review a diff you are still writing.
- The worst defect class here is a path that can produce clinical text without a citation. Look for it first.
