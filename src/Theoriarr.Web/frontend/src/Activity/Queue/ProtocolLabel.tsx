import React from 'react';
import Label from 'Components/Label';
import DownloadProtocol from 'DownloadClient/DownloadProtocol';

const PROTOCOL_CLASSES: Record<DownloadProtocol, string> = {
  torrent:
    'inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default border-[var(--torrentColor)] bg-[var(--torrentColor)]',
  unknown:
    'inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default',
  usenet:
    'inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default border-[var(--usenetColor)] bg-[var(--usenetColor)]',
};

interface ProtocolLabelProps {
  protocol: DownloadProtocol;
}

function ProtocolLabel({ protocol }: ProtocolLabelProps) {
  const protocolName = protocol === 'usenet' ? 'nzb' : protocol;

  return <Label className={PROTOCOL_CLASSES[protocol]}>{protocolName}</Label>;
}

export default ProtocolLabel;
