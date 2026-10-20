# C4 Model — SearchSystem

Four levels, one file each, each zooming into the previous one:

1. [System Context](01-system-context.md) — SearchSystem as a black box, its users, its one external dependency.
2. [Containers](02-containers.md) — the independently runnable pieces (API shards, the LoadBalancer coordinator, indexer, both clients) and how they talk over HTTP.
3. [Components](03-components.md) — inside API, LoadBalancer, and indexer: the classes/interfaces that make each one up.
4. [Code](04-code.md) — class diagrams and one sequence diagram for the search logic and the scatter-gather coordinator.

Written against branch `y-skalering-DB` (commit `57cadaf`). The top-level `CLAUDE.md`/`AGENTS.md` predate
this branch's sharding work — these four files reflect the code as it actually is now, including the fact
that `SearchCore/`/`SearchRepository/` are dead folders, not live projects (see
[02-containers.md](02-containers.md#a-dead-end-worth-knowing-about)).
