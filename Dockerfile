FROM rabbitmq:3-management as rabbitmq

RUN rabbitmq-plugins enable rabbitmq_amqp1_0 && \
    printf "amqp1_0.default_user = guest\n" > $RABBITMQ_HOME/etc/rabbitmq/rabbitmq.conf && \
    printf "amqp1_0.default_vhost = /\n" >> $RABBITMQ_HOME/etc/rabbitmq/rabbitmq.conf && \
    printf "amqp1_0.protocol_strict_mode = false\n" >> $RABBITMQ_HOME/etc/rabbitmq/rabbitmq.conf && \
    chmod 644 $RABBITMQ_HOME/etc/rabbitmq/rabbitmq.conf && chown rabbitmq:rabbitmq $RABBITMQ_HOME/etc/rabbitmq/rabbitmq.conf
