FROM postgres:16-alpine@sha256:721873c34ceb9f8d8fc265984940dc982404c105f19ad51be9fdc5970a6080ea

# Apply distro security fixes. Replace the upstream Go-based gosu helper
# with Alpine's small C helper and update the official entrypoint.
RUN apk upgrade --no-cache \
    && apk add --no-cache su-exec \
    && sed -i 's/exec gosu postgres/exec su-exec postgres/' /usr/local/bin/docker-entrypoint.sh \
    && rm /usr/local/bin/gosu
