import {FrameElement} from "./FrameElement.js";

export class FrameContainer {
    constructor(id) {
        this.id = id;
        this.frames = [];
    }

    get element() { return document.getElementById(this.id); }

    clear() { 
        this.frames = [];
    }

    setSize(width, height) {
        this.element.width = width;
        this.element.height = height;
    }

    addFrame(frame) {
        const frameElement = new FrameElement(frame);
        this.frames.push(frameElement);
        this.element.appendChild(frameElement.element);
    }
    
    async update() {
        for (const f of this.frames) {
            await f.update();
        }
    }
}