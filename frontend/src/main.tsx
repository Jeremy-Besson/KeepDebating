import React from "react";
import ReactDOM from "react-dom/client";
import { HashRouter, Route, Routes } from "react-router-dom";
import App from "./App";
import DebatePage from "./pages/DebatePage";
import IntroPage from "./pages/IntroPage";
import "./styles.css";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <HashRouter>
      <Routes>
        <Route path="/" element={<App />}>
          <Route index element={<DebatePage />} />
          <Route path="intro" element={<IntroPage />} />
        </Route>
      </Routes>
    </HashRouter>
  </React.StrictMode>,
);
