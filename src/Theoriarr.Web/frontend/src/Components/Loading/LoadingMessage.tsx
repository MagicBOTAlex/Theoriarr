import React from 'react';

const messages = [
  'Downloading more RAM',
  'Now in Technicolor',
  'Loading Theoriarr...',
  'Bleep Bloop.',
  'Locating the required gigapixels to render...',
  'Spinning up the hamster wheel...',
  "At least you're not on hold",
  'Hum something loud while others stare',
  'Loading humorous message... Please Wait',
  "I could've been faster in Python",
  "Don't forget to rewind your episodes",
  'Congratulations! You are the 1000th visitor.',
  'Re-calibrating the internet...',
  "I'll be here all week",
  "Don't forget to tip your waitress",
  'Apply directly to the forehead',
  'Loading Battlestation',
];

let message: string | null = null;

function LoadingMessage() {
  if (!message) {
    const index = Math.floor(Math.random() * messages.length);
    message = messages[index];
  }

  return (
    <div className="mx-2.5 mt-2.5 text-center text-[36px] font-light">
      {message}
    </div>
  );
}

export default LoadingMessage;
