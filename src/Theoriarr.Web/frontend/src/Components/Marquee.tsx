import classNames from 'classnames';
import React, { Component } from 'react';

const FPS = 20;
const STEP = 1;
const TIMEOUT = (1 / FPS) * 1000;

interface MarqueeProps {
  className?: string;
  text?: string;
  title?: string;
  hoverToStop?: boolean;
  loop?: boolean;
}

interface MarqueeState {
  animatedWidth: number;
  overflowWidth: number;
  direction: number;
}

class Marquee extends Component<MarqueeProps, MarqueeState> {
  state: MarqueeState = {
    animatedWidth: 0,
    overflowWidth: 0,
    direction: 0,
  };

  componentDidMount() {
    this.measureText();
    this.syncAnimation();
  }

  componentDidUpdate(prevProps: MarqueeProps, prevState: MarqueeState) {
    if (prevProps.text !== this.props.text) {
      this.stopAnimation();
      this.setState({ animatedWidth: 0, direction: 0 });
      return;
    }

    this.measureText();

    // Only (re)start when the measured overflow changes. Restarting on every
    // render is what made the span jitter back and forth between pixels even
    // when the text fits.
    if (prevState.overflowWidth !== this.state.overflowWidth) {
      this.syncAnimation();
    }
  }

  componentWillUnmount() {
    this.stopAnimation();
  }

  private container: HTMLDivElement | null = null;
  private text: HTMLSpanElement | null = null;
  private marqueeTimer?: ReturnType<typeof setTimeout>;
  private isHovered = false;

  // Start the marquee when the text actually overflows and it should be
  // running, otherwise stop and snap back to the start.
  syncAnimation = () => {
    const shouldAnimate =
      this.state.overflowWidth > 0 &&
      (this.props.hoverToStop === false || !this.isHovered);

    if (shouldAnimate) {
      this.startAnimation();
    } else {
      this.stopAnimation();

      if (this.state.animatedWidth !== 0) {
        this.setState({ animatedWidth: 0, direction: 0 });
      }
    }
  };

  startAnimation = () => {
    this.stopAnimation();

    if (this.state.overflowWidth <= 0) {
      return;
    }

    // Bounce between 0 and the overflow width instead of letting the offset
    // wander past both ends.
    const animate = () => {
      const { overflowWidth, animatedWidth, direction } = this.state;
      const nextWidth = animatedWidth + (direction === 0 ? STEP : -STEP);

      let updatedWidth = nextWidth;
      let updatedDirection = direction;

      if (nextWidth >= overflowWidth) {
        updatedWidth = overflowWidth;
        updatedDirection = 1;
      } else if (nextWidth <= 0) {
        updatedWidth = 0;
        updatedDirection = 0;
      }

      this.setState({
        animatedWidth: updatedWidth,
        direction: updatedDirection,
      });
      this.marqueeTimer = setTimeout(animate, TIMEOUT);
    };

    this.marqueeTimer = setTimeout(animate, TIMEOUT);
  };

  stopAnimation = () => {
    clearTimeout(this.marqueeTimer);
  };

  measureText = () => {
    const container = this.container;
    const node = this.text;

    if (container && node) {
      const containerWidth = container.offsetWidth;
      const textWidth = node.offsetWidth;
      const overflowWidth = textWidth - containerWidth;

      if (overflowWidth !== this.state.overflowWidth) {
        this.setState({ overflowWidth });
      }
    }
  };

  onHandleMouseEnter = () => {
    this.isHovered = true;

    if (this.props.hoverToStop !== false) {
      this.stopAnimation();

      if (this.state.animatedWidth !== 0) {
        this.setState({ animatedWidth: 0, direction: 0 });
      }
    }
  };

  onHandleMouseLeave = () => {
    this.isHovered = false;
    this.syncAnimation();
  };

  render() {
    const { className = '', text = '', title = '' } = this.props;

    const style = {
      position: 'relative' as const,
      right: this.state.animatedWidth,
      whiteSpace: 'nowrap' as const,
    };

    const titleText =
      title && text !== title ? `Original Title: ${title}` : text;

    if (this.state.overflowWidth < 0) {
      return (
        <div
          ref={(el) => {
            this.container = el;
          }}
          className={classNames('ui-marquee', className)}
          style={{ overflow: 'hidden' }}
        >
          <span
            ref={(el) => {
              this.text = el;
            }}
            style={style}
            title={titleText}
          >
            {text}
          </span>
        </div>
      );
    }

    return (
      <div
        ref={(el) => {
          this.container = el;
        }}
        className={classNames('ui-marquee', className)}
        style={{ overflow: 'hidden' }}
        onMouseEnter={this.onHandleMouseEnter}
        onMouseLeave={this.onHandleMouseLeave}
      >
        <span
          ref={(el) => {
            this.text = el;
          }}
          style={style}
          title={titleText}
        >
          {text}
        </span>
      </div>
    );
  }
}

export default Marquee;
