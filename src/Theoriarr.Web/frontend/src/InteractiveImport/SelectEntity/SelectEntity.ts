import React from 'react';

// Minimal shape shared by every selectable library entity (movie, series, ...).
export interface SelectEntityItem {
  id: number;
  title: string;
  sortTitle: string;
  year: number;
  imdbId?: string;
}

export interface SelectEntityColumn<T = SelectEntityItem> {
  name: string;
  label: string | (() => string);
  isVisible?: boolean;
  render(item: T): React.ReactNode;
}
