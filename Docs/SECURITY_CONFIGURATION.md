# Production security configuration

The API uses JWT bearer authentication outside the isolated `Testing` environment. Production startup requires `Auth:Issuer`, `Auth:Authority`, and `Auth:Audience`; issuer and authority must be HTTPS URLs. Signing keys are obtained from the configured OIDC authority, and startup verifies the discovered issuer and signing keys before the API begins serving readiness checks. The test-only `Auth:TestSigningKey` is rejected outside `Testing`.

Production CORS requires one or more explicit HTTPS origins in `Cors:AllowedOrigins`. Wildcards, HTTP origins, and implicit localhost defaults are rejected. Development retains the localhost origin default. The API redirects HTTP to HTTPS and enables HSTS in production.

Forwarded headers are ignored by default. When TLS is terminated at a reverse proxy, enable `ForwardedHeaders:Enabled` and configure every trusted immediate proxy IP under `ForwardedHeaders:KnownProxies`. The API accepts one forwarded hop and does not trust headers from arbitrary clients. Without this explicit configuration, terminate TLS directly at the API.

Supply these values through the deployment's protected configuration/secret mechanism; do not commit signing secrets or database credentials. The API does not accept a locally configured production signing secret.
