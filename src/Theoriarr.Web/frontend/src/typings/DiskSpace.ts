interface DiskSpace {
  path: string;
  label: string;
  freeSpace: number;
  totalSpace: number;
}

export interface DiskSpaceContent {
  path: string;
  name: string;
  isFile: boolean;
  size: number;
}

export default DiskSpace;
