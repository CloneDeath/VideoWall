import {FrameApi} from "./FrameApi.js";

export class Api {
    async getFrames() {
        const response = await fetch("/VideoWall/Frames");
        const data = await response.json();
        return data.map(d => new FrameApi(d));
    }
}