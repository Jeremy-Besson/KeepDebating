import { useState } from "react";
import { Outlet, useLocation, useNavigate } from "react-router-dom";

export interface LayoutOutletContext {
  setFocusConversation: (value: boolean) => void;
}

export default function App() {
  const location = useLocation();
  const navigate = useNavigate();
  const isIntroPage = location.pathname === "/intro";
  const [focusConversation, setFocusConversation] = useState(false);

  return (
    <div
      className={`max-w-4xl mx-auto p-4 grid gap-3 
        ${focusConversation ? "h-screen overflow-hidden grid-rows-[auto_1fr]" : "auto-rows-max"}`}
    >
      <header className="flex items-center justify-between gap-3">
        <div className="flex items-center gap-3 min-w-0">
          <h1 className="brand-wordmark text-4xl leading-none flex-shrink-0 m-0">
            KeepDebating
          </h1>
          <p className="text-lg text-slate-600 whitespace-nowrap overflow-hidden text-ellipsis">
            Run the debate and inspect spectator votes in one place.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          {isIntroPage ? (
            <button
              type="button"
              className="btn-secondary"
              onClick={() => navigate("/")}
            >
              Back to Debate
            </button>
          ) : (
            <button
              type="button"
              className="btn-secondary"
              onClick={() => navigate("/intro")}
            >
              Intro
            </button>
          )}
        </div>
      </header>
      <main
        className={
          focusConversation
            ? "grid gap-3 min-h-0 overflow-hidden grid-rows-[auto_minmax(0,1fr)]"
            : "grid gap-3 content-start"
        }
      >
        <Outlet context={{ setFocusConversation } as LayoutOutletContext} />
      </main>
    </div>
  );
}
