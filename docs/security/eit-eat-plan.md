# EIT & EAT — Entity Identity Token / Entity Authorization Token

**Status: PLANNED — target pre-alpha. Not implemented (2026-08-30).**
This is the design agreement for the post-MVP security mission. Do not implement until
pre-alpha kicks off; revisit the open decisions below when you do.

## Problem with the current scheme

Requests are validated with a shared client API key (`ClientRequestSecurityService`
`ValidateClientSecurityAsync`, with a dev pepper fallback in `DevCredentials`). One key
for everything means:

- no per-entity identity (who is acting, on which session),
- no per-operation authorization (a world-read gem and a submit-action gem need the same key),
- no granular revocation or rotation,
- a leaked key is a full compromise.

## Vision

Two short-lived, signed token types binding identity and authorization, in the spirit of
modern token engineering (JOSE/RFC 7519, CWT/RFC 8392, SPICE/CEK, OAuth 2.1 style
proof-of-possession), tuned for game entities:

1. **EIT — Entity Identity Token.** Proves *who* an entity is and which world session it is
   on. Single-session-bound, refreshable at half TTL.
2. **EAT — Entity Authorization Token.** Proves *what* that entity may do. Capability/scoped
   grant minted per operation and bound to the presenting EIT (via its `jti`), very short-lived.

Tier one (read) and tier two (actions) lifetimes differ so action tokens die fast; read tokens
live a little longer and are cached in Redis.

## Token shapes

EIT (JWT serialized initially; CBOR/CWT later if packet size matters):

```
{ typ: "eit+jwt", eit_ver: 1,
  sub: "player/000001-aaaa", sid: "session-7f3c...", tier: "player",
  iss: "auth.mmo", iat, exp, jti, nonce }
```

EAT:

```
{ typ: "eat+jwt", eat_ver: 1,
  sub, sid, eit_jti: "<EIT jti>", caps: ["actions:submit:<sid>", "world:sync:<sid>"],
  iss, iat, exp, nonce, cnf?: { tls: <session binding> } }
```

## Capability vocabulary

| Cap | Grants |
|-----|--------|
| `actions:submit:<sid>` | submit `POST /v1/actions/{sessionId}` for that session |
| `world:read:<sid>` | REST/GraphQL world+state reads for that session |
| `world:sync:<sid>` | binary snapshot sync / websocket updates |
| `agent:manage:<sid>` | agent task operations for the session |
| `admin:observability` / `admin:pipeline` | `/admin` routes |

## Issuance flow (pre-alpha)

1. Client authenticates with `/v1/auth/login|register/...` (existing routes) -> token
   service issues EIT.
2. Client requests an operation -> policy layer evaluates tier+permissions -> issues an EAT
   bound to the EIT `jti`; Redis caches EIT->EAT grants with short TTL.
3. Authoritative actions and `/graphql` validate: signature, freshness (with clock-skew grace),
   `eit_jti` binding, required cap, single-session, and per-action `nonce` replay checks.
4. Rotation at half TTL; revocation via Redis revoke-list keyed by `jti` (session change,
   logout, privilege drop, security event). Action idempotency keeps using `actionId`/`expectedTurn`.

## Where it plugs in

- Upgrade `ClientRequestSecurityService.ValidateClientSecurityAsync` to validate EIT+EAT
  instead of the shared key.
- Enforce caps on `/graphql` (already gated) and on `/v1/actions/*`.
- Keep the existing idempotency/replay layer unchanged.

## Security properties (acceptance checklist for pre-alpha)

- [ ] Least privilege: every token carries only the caps the operation needs
- [ ] Freshness: short exp; clock-skew grace bounded (e.g., ±30 s)
- [ ] Binding: EAT references presenting EIT `jti`; single-session enforced
- [ ] Replay protection: per-action `nonce`/`actionId` dedupe
- [ ] Revocation: pre-expiry revoke via Redis on `jti`
- [ ] No shared-secret blast radius; per-entity audit trail of issuance/use

## Open decisions (resolve before implementing)

- Payload format: JWT first (tooling) vs CWT/CBOR (size). Recommend JWT, add CWT at wire-size pain point.
- Signing: Ed25519 (small keys/handles) vs ES256. Define key-rotation interval.
- Where policy lives: in `services/authoritative` vs a dedicated auth service (recommend a separate auth container for pre-alpha).
- PoP level: TLS-session binding first (`cnf.tls`), key-bound later.
- Naming note: "EAT" collides with IETF RATS "Entity Attestation Token". If confusion matters, rename to e.g. CIT/CAT before committing to tooling.

## Milestones

- **P1** (pre-alpha start): EIT issuance + validation middleware + EAT capability checks on
  actions and GraphQL + Redis revocation + backend tests.
- **P2**: dedicated policy service, refresh/rotation UX, issuance audit log.
- **P3**: attestation claims (RATS/EAT style), external identity providers, multi-tenant.

The acceptance gate for P1: every `/v1/actions/*` and `/graphql` request must present a
valid EIT bound EAT with the exact required cap; a token minted for session A cannot touch
session B; revocation takes effect < 1 s; replay of a consumed action id is rejected.