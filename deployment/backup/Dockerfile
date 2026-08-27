FROM public.ecr.aws/aws-cli/aws-cli:2.27.49@sha256:bad3346a39098ab077be6ed58c7e1fe68321a4a844c7c740318100013e6c3581 AS awscli

FROM mysql:8.4.10@sha256:c592c15aaf4a1961e15d82eb31ea5987dda862d1c4b1e93424438c0e91dc1f8d
RUN microdnf install --assumeyes findutils gzip \
    && microdnf clean all

COPY --from=awscli /usr/local/aws-cli /usr/local/aws-cli
COPY backup-entrypoint.sh s3-write-check.sh /usr/local/bin/
RUN ln --symbolic /usr/local/aws-cli/v2/current/bin/aws /usr/local/bin/aws \
    && chmod 755 /usr/local/bin/backup-entrypoint.sh /usr/local/bin/s3-write-check.sh

VOLUME ["/backups"]
ENTRYPOINT ["/usr/local/bin/backup-entrypoint.sh"]
