# Security policy

## Supported versions

FamilyNido is a single-family self-hosted PWA. Only the `main` branch is
supported — there are no released "versions" to back-port fixes to.

## Reporting a vulnerability

If you find something that looks like a security issue, please **do not**
open a public GitHub issue. Instead:

1. Use GitHub's *Report a vulnerability* button on the **Security** tab of
   this repository (private disclosure to maintainers).
2. Or email the maintainer directly via the address on the GitHub profile
   linked from the commit history.

Please include enough detail to reproduce: affected endpoint(s), inputs,
expected vs. actual behaviour, and any logs you can share. A short
proof-of-concept goes a long way.

You can expect:

- An acknowledgement within a few days.
- A fix or a clear "won't fix with reason" within two weeks for
  high-severity issues, longer for low-severity ones.
- A public credit in the commit message if you want one.

## Out of scope

- DDoS, volumetric or resource-exhaustion attacks against the
  reference deployment. Rate limiting is in place but the project is not
  designed to survive large-scale attacks.
- Issues that require physical access to the server hosting the instance.
- Vulnerabilities in upstream dependencies that already have a published
  CVE — open a regular issue (or PR with the bump) instead.
- Social-engineering or anything that targets an operator rather than the
  software itself.

## Threat model in one paragraph

FamilyNido is intended to run on a home server behind a reverse proxy that
terminates TLS (typically Traefik), accessible to the household members
through their own browsers and to a handful of integrations (Home
Assistant, etc.) through API keys. The realistic attackers we worry about
are: a curious neighbour on the LAN, a guest the family briefly granted
access to, and the open internet poking at the public URL once the
service is exposed. Strong assumptions: the operator controls the host,
keeps the OS patched, and runs the `prod.yml` stack instead of editing
containers by hand.
