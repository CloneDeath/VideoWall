import {FrameContainer} from "./FrameContainer.js";
import {Api} from "./Api/Api.js";

const api = new Api();
const frames = await api.getFrames();

const frameContainer = new FrameContainer("frame-container");
frameContainer.clear();
frames.forEach(f => {
    frameContainer.addFrame(f);
});