FROM public.ecr.aws/aws-cli/aws-cli:2.27.49 AS awscli

FROM mysql:8.4.10
RUN microdnf install --assumeyes findutils gzip \
    && microdnf clean all

COPY --from=awscli /usr/local/aws-cli /usr/local/aws-cli
COPY backup-entrypoint.sh s3-write-check.sh /usr/local/bin/
RUN ln --symbolic /usr/local/aws-cli/v2/current/bin/aws /usr/local/bin/aws \
    && chmod 755 /usr/local/bin/backup-entrypoint.sh /usr/local/bin/s3-write-check.sh

VOLUME ["/backups"]
ENTRYPOINT ["/usr/local/bin/backup-entrypoint.sh"]
