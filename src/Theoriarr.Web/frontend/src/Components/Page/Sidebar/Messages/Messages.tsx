import React, { useMemo } from 'react';
import { Message as MessageModel, useMessages } from 'App/messagesStore';
import Message from './Message';

function Messages() {
  const items = useMessages();

  const messages = useMemo(() => {
    return items.reduce<MessageModel[]>((acc, item) => {
      acc.unshift(item);

      return acc;
    }, []);
  }, [items]);

  return (
    <div className="mt-auto mb-5 pt-5 max-md:mb-0">
      {messages.map((message) => {
        return <Message key={message.id} {...message} />;
      })}
    </div>
  );
}

export default Messages;
