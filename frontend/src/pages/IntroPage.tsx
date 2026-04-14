import React, { useState } from "react";

const TASKS = [
  { done: true, text: "Clone the main task and all the sub-tasks" },
  { done: true, text: "Register to Azure - Microsoft Foundry" },
  {
    done: true,
    text: "Learning the AI fundamentals and the tools to create an LLM Agent",
    sub: [
      {
        done: true,
        text: "Review the AI fundamentals (architecture, agents, tools, reasoning loops).",
      },
      {
        done: true,
        text: "Complete the initial set of foundational labs covering agents, prompt engineering, embeddings, and RAG.",
      },
      {
        done: true,
        text: "microsoft.com/en-us/training/paths/develop-generative-ai-apps/",
      },
    ],
  },
  {
    done: true,
    text: "Set up the local or cloud environment using resources from any provider or local infrastructure.",
  },
  {
    done: true,
    text: "Find what AI application will be implemented in the POC (Debaters, AutoGPT, etc.).",
  },
  {
    done: true,
    text: "Find what LLM Agent framework to use for the POC (LangSmith, LangChain, Microsoft Bot Framework, etc.).",
  },
  {
    done: true,
    text: "Design an initial architecture of the POC, including the components, data flow, and integration points.",
  },
  { done: true, text: "Implement the first version of the POC." },
];

const KEY_POINTS = [
  "Enjoy and Learn",
  "All-in vibe coding: use Copilot (EPAM license) and not write a single line of code by hand.",
  "The POC should helps to practice different core aspects of agentic agent development: context, memory, failures handling, tool integration, agent orchestration, RAG, human in the loop,...",
  "Use cheap Azure OpenAI (GPT-4o) as the main LLM for the agent.",
  "Use free GPT-4.1 free model in copilot as much as possible to save tokens.",
  "Use chat.lab.epam.com and free Gemini LLM chat as much as possible to save Copilot tokens.",
  "Tech. stack: C# .NET 10 for the backend and React/Vite/TypeScript/TailwindCSS for the frontend.",
  "Deployment in Azure is not required, local development is perfectly fine.",
  "No Semantic Kernel/Agent Framework Layer first to get my hand dirty with Microsoft.Extensions.AI for agent orchestration and tool integration and maybe later Microsoft Agent Framework.",
];

const VERSIONS = [
  {
    title: "First version: kind-of-working",
    items: [
      "Each debater talks for some predefined rounds.",
      'The topic is fixed: "Is Tamagotchi good for kids?"',
      "Hard-coded Tamagotchi data used as a tool to support arguments.",
    ],
  },
  {
    title:
      "Second version: Add the Brain Orchestrator with C# Agent Framework - Microsoft.SemanticKernel;",
    items: [
      "Migrate orchestration to Microsoft Agent Framework.",
      "Replace the hard-coded flow with an LLM Brain Orchestrator, the core component of an agentic system, responsible for managing the overall behavior and decision-making processes of the agents. It acts as the central hub that coordinates the interactions between different agents, tools, and data sources to achieve the desired outcomes.",
    ],
  },
  {
    title:
      "Third version: Use Wikipedia WebApi for more dynamic data retrieval",
    items: [
      "Use Wikipedia WebApi.",
      "Add a tool to the Brain Orchestrator.",
      "Debaters dynamically retrieve data from the tool based on the topic and arguments.",
    ],
  },
  {
    title: "Fourth version: Human in the Loop",
    items: [
      "The user can be involved in the discussion by the orchestrator.",
      "Use SignalR instead of SSE for real-time bidirectional communication between the browser and the orchestrator, allowing the user to interact with the debaters while they are still thinking.",
      "Add it to the orchestator as another tool to be used by the debaters.",
    ],
  },
  {
    title: "Fifth version: RAG and Memory",
    items: [
      "Integrate RAG (Retrieval-Augmented Generation) and memory capabilities into the orchestrator.",
    ],
  },
];

type ImageProps = {
  src: string;
  alt: string;
  maxWidth?: string;
};

type CheckIconProps = {
  done: boolean;
};

// Reusable wrapper for images to control size
function ImageWrapper({ src, alt, maxWidth = "max-w-md" }: ImageProps) {
  return (
    <div
      className={`flex justify-center my-4 overflow-hidden rounded-xl border border-slate-200 bg-slate-50 p-2 mx-auto ${maxWidth}`}
    >
      <img src={src} alt={alt} className="w-full h-auto object-contain" />
    </div>
  );
}

function ImagePlainLarge({ src, alt }: Omit<ImageProps, "maxWidth">) {
  return (
    <div className="my-5 mx-auto max-w-3xl">
      <img src={src} alt={alt} className="w-full h-auto object-contain" />
    </div>
  );
}

function CheckIcon({ done }: CheckIconProps) {
  return done ? (
    <span className="inline-flex items-center justify-center w-4 h-4 rounded-sm bg-green-600 text-white flex-shrink-0 text-[10px] font-bold">
      ✓
    </span>
  ) : (
    <span className="inline-flex items-center justify-center w-4 h-4 rounded-sm border border-slate-300 bg-white flex-shrink-0" />
  );
}

export default function App() {
  const [activeTab, setActiveTab] = useState("first-week");

  return (
    <div className="max-w-4xl mx-auto p-6 grid gap-6 text-slate-800">
      {/* Tab Navigation */}
      <div className="border-b border-slate-200">
        <div className="flex gap-4">
          <button
            onClick={() => setActiveTab("first-week")}
            className={`px-4 py-3 font-semibold border-b-2 transition-colors ${
              activeTab === "first-week"
                ? "border-blue-600 text-blue-600"
                : "border-transparent text-slate-600 hover:text-slate-800"
            }`}
          >
            Intro - First week
          </button>
          <button
            onClick={() => setActiveTab("second-week")}
            className={`px-4 py-3 font-semibold border-b-2 transition-colors ${
              activeTab === "second-week"
                ? "border-blue-600 text-blue-600"
                : "border-transparent text-slate-600 hover:text-slate-800"
            }`}
          >
            Second week
          </button>
        </div>
      </div>

      {/* First Week Tab */}
      {activeTab === "first-week" && (
        <>
          <ImageWrapper
            src="/ComicAllInOne.png"
            alt="Learning Domain Driven Design Comic"
            maxWidth="max-w-2xl"
          />

          {/* KEY POINTS */}
          <section className="bg-white rounded-xl shadow-sm border p-5">
            <h2 className="text-xl font-bold mb-3 border-l-4 border-blue-600 pl-3">
              Key Points
            </h2>
            <ul className="grid gap-2 pl-5 list-disc text-base">
              {KEY_POINTS.map((pt, i) => (
                <li key={i} className="leading-relaxed">
                  {pt}
                </li>
              ))}
            </ul>
          </section>

          {/* POC Idea */}
          <section className="bg-white rounded-xl shadow-sm border p-5">
            <h2 className="text-xl font-bold mb-3 border-l-4 border-green-600 pl-3">
              POC Idea -{" "}
              <span className="brand-wordmark text-2xl">KeepDebating</span>
            </h2>
            <div className="space-y-3 text-base text-slate-700">
              <p>
                The user provides a Yes/No question; two AI agents (For/Against)
                debate with distinct personalities.
              </p>
              <div className="grid gap-3 sm:grid-cols-2 mt-4">
                {VERSIONS.map((v, i) => (
                  <div
                    key={i}
                    className="border border-slate-100 rounded-lg p-3 bg-slate-50"
                  >
                    <h3 className="font-semibold text-slate-800 flex items-center mb-1">
                      <span className="w-5 h-5 rounded-full bg-blue-600 text-white text-[10px] flex items-center justify-center mr-2">
                        {i + 1}
                      </span>
                      {v.title}
                    </h3>
                    <ul className="pl-4 list-disc text-sm text-slate-600">
                      {v.items.map((item, j) => (
                        <li key={j}>{item}</li>
                      ))}
                    </ul>
                  </div>
                ))}
              </div>
            </div>
          </section>

          {/* Diagrams Section */}
          <section className="bg-white rounded-xl shadow-sm border p-5">
            <h2 className="text-xl font-bold mb-4 border-l-4 border-purple-600 pl-3">
              Technical Visuals
            </h2>

            <div className="space-y-8">
              <div>
                <h3 className="text-sm font-semibold text-slate-500 uppercase tracking-wider mb-2">
                  Learning Path
                </h3>
                <ImageWrapper
                  src="/Learning1.png"
                  alt="Learning 1"
                  maxWidth="max-w-sm"
                />
                <ImageWrapper
                  src="/Learning2.png"
                  alt="Learning 2"
                  maxWidth="max-w-sm"
                />
                <ImageWrapper
                  src="/Learning3.png"
                  alt="Learning 3"
                  maxWidth="max-w-sm"
                />
              </div>

              <div>
                <h3 className="text-sm font-semibold text-slate-500 uppercase tracking-wider mb-2">
                  System Architecture
                </h3>
                <ImageWrapper
                  src="/architecture.png"
                  alt="Architecture"
                  maxWidth="max-w-lg"
                />
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <h3 className="text-sm font-semibold text-slate-500 uppercase tracking-wider mb-2">
                    Azure Token Usage
                  </h3>
                  <ImageWrapper
                    src="/Azure_Tokens.png"
                    alt="Azure Tokens"
                    maxWidth="max-w-xs"
                  />
                </div>
                <div>
                  <h3 className="text-sm font-semibold text-slate-500 uppercase tracking-wider mb-2">
                    Copilot Usage
                  </h3>
                  <ImageWrapper
                    src="/Copilot_Tokens.png"
                    alt="Copilot Tokens"
                    maxWidth="max-w-xs"
                  />
                </div>
              </div>
            </div>
          </section>

          {/* Tasks */}
          <section className="bg-white rounded-xl shadow-sm border p-5">
            <h2 className="text-xl font-bold mb-3 border-l-4 border-orange-600 pl-3">
              Tasks - First Week
            </h2>
            <ul className="space-y-3 mt-4">
              {TASKS.map((task, i) => (
                <li key={i}>
                  <div
                    className={`flex items-start gap-3 ${task.done ? "text-slate-400" : "text-slate-800"}`}
                  >
                    <CheckIcon done={task.done} />
                    <span className={task.done ? "line-through" : ""}>
                      {task.text}
                    </span>
                  </div>
                  {task.sub && (
                    <ul className="mt-2 ml-8 space-y-2">
                      {task.sub.map((s, j) => (
                        <li
                          key={j}
                          className={`flex items-start gap-3 text-sm ${s.done ? "text-slate-300" : "text-slate-500"}`}
                        >
                          <CheckIcon done={s.done} />
                          <span className={s.done ? "line-through" : ""}>
                            {s.text}
                          </span>
                        </li>
                      ))}
                    </ul>
                  )}
                </li>
              ))}
            </ul>
          </section>
        </>
      )}

      {/* Second Week Tab */}
      {activeTab === "second-week" && (
        <>
          <ImageWrapper
            src="/Week2Status.png"
            alt="Week 2 Status"
            maxWidth="max-w-2xl"
          />

          <section className="bg-white rounded-xl shadow-sm border p-5">
            <h2 className="text-xl font-bold mb-3 border-l-4 border-purple-600 pl-3">
              Public Github repository for the POC - KeepDebating
            </h2>
            <a
              href="https://github.com/Jeremy-Besson/KeepDebating"
              target="_blank"
              rel="noopener noreferrer"
              className="text-blue-600 hover:text-blue-800 font-medium underline transition-colors duration-200"
            >
              https://github.com/Jeremy-Besson/KeepDebating
            </a>
          </section>

          <section className="bg-white rounded-xl shadow-sm border p-5">
            <h2 className="text-xl font-bold mb-3 border-l-4 border-green-600 pl-3">
              Tasks - Second Week
            </h2>

            <div className="space-y-4 text-sm text-slate-700">
              <div>
                <h3 className="font-semibold text-slate-900 mb-1">
                  PoC Idea & Target User Scenario
                </h3>
                <ul className="list-disc pl-5 space-y-1">
                  <li>PoC name: KeepDebating AI Agent.</li>
                  <li>
                    Scenario: user enters a topic and gets a structured pro/con
                    debate from two AI personas.
                  </li>
                  <li>
                    Target users: internal team members, tech leads, and product
                    stakeholders.
                  </li>
                  <li>
                    Usage context: quick decision support and trade-off
                    exploration before implementation choices.
                  </li>
                </ul>
              </div>

              <div>
                <h3 className="font-semibold text-slate-900 mb-1">
                  Problem & Expected Value
                </h3>
                <ul className="list-disc pl-5 space-y-1">
                  <li>
                    Problem: teams spend too much time collecting pros/cons
                    manually and discussions can be biased or incomplete.
                  </li>
                  <li>
                    Expected value: generate balanced viewpoints quickly with
                    evidence-backed arguments.
                  </li>
                  <li>
                    Expected value: improve discussion quality through
                    structured turns and clear reasoning.
                  </li>
                  <li>
                    Expected value: provide a reusable base for future agentic
                    features (memory, RAG, and multi-tool orchestration).
                  </li>
                </ul>
              </div>

              <div>
                <h3 className="font-semibold text-slate-900 mb-1">
                  Planned AI Capabilities & Platform Interactions
                </h3>
                <ul className="list-disc pl-5 space-y-1">
                  <li>
                    Dual-agent debate orchestration (Pro and Con personas).
                  </li>
                  <li>
                    Brain orchestrator decides next step: next turn, follow-up,
                    or ask human.
                  </li>
                  <li>
                    Dynamic evidence retrieval from Wikipedia during argument
                    generation.
                  </li>
                  <li>Human-in-the-loop checkpoint when context is missing.</li>
                  <li>Session transcript and verdict summary generation.</li>
                  <li>
                    Frontend sends topic/settings; backend orchestrates turns,
                    tools, and model calls.
                  </li>
                  <li>
                    Support streaming/incremental updates for near real-time UX.
                  </li>
                </ul>
              </div>

              <div>
                <h3 className="font-semibold text-slate-900 mb-1">
                  Data, Tools & Integrations
                </h3>
                <ul className="list-disc pl-5 space-y-1">
                  <li>
                    Data inputs: topic, rounds, selected personas, and optional
                    human feedback.
                  </li>
                  <li>
                    Data outputs: transcript, turn-level reasoning metadata, and
                    final summary/verdict.
                  </li>
                  <li>
                    LLM platform: Azure OpenAI for reasoning and turn
                    generation.
                  </li>
                  <li>
                    Tooling: Wikipedia Web API for external factual grounding.
                  </li>
                  <li>Tech stack: .NET 10 backend and React/Vite frontend.</li>
                  <li>
                    Optional next integrations: SignalR for bidirectional
                    real-time interaction and vector store for memory/RAG.
                  </li>
                </ul>
              </div>

              <div>
                <h3 className="font-semibold text-slate-900 mb-1">
                  Why This PoC Is Meaningful
                </h3>
                <ul className="list-disc pl-5 space-y-1">
                  <li>
                    Demonstrates practical agentic patterns beyond a single
                    prompt-response flow.
                  </li>
                  <li>
                    Covers orchestration, tool use, grounding, and human
                    oversight in one workflow.
                  </li>
                  <li>
                    Delivers visible business value quickly with low
                    infrastructure complexity.
                  </li>
                  <li>
                    Creates a clear path for phase 2 enhancements (RAG, memory,
                    reliability, and evaluation).
                  </li>
                </ul>
              </div>
            </div>
          </section>

          <section className="bg-white rounded-xl shadow-sm border p-5">
            <h2 className="text-xl font-bold mb-4 border-l-4 border-cyan-600 pl-3">
              Feature Wikipedia API implementation with Github Copilot - Vibe
              coding!!!
            </h2>

            <div className="space-y-8">
              <div>
                <h3 className="text-sm font-semibold text-slate-500 uppercase tracking-wider mb-2">
                  Prompt
                </h3>

                <div className="rounded-lg border border-slate-200 bg-slate-50 p-4 mb-4 whitespace-pre-line text-sm text-slate-700 leading-relaxed">
                  {`Feature: Use Wikipedia WebApi for more dynamic data retrieval

Use Wikipedia WebApi.
Add a tool to the Brain Orchestrator.
Debaters dynamically retrieve data from the tool based on the topic and arguments.

Analyze the requirements.
Ask questions to clarify if needed.
Plan and output the plan.

Don't implement yet.`}
                </div>

                <ImagePlainLarge
                  src="/CopilotFeatureStep1.png"
                  alt="Copilot Feature Step 1"
                />
                <ImagePlainLarge
                  src="/CopilotFeatureStep2.png"
                  alt="Copilot Feature Step 2"
                />
                <ImagePlainLarge
                  src="/CopilotFeatureStep3.png"
                  alt="Copilot Feature Step 3"
                />
                <ImagePlainLarge
                  src="/CopilotFeatureStep4.png"
                  alt="Copilot Feature Step 4"
                />
                <ImagePlainLarge
                  src="/CopilotFeatureStep5.png"
                  alt="Copilot Feature Step 5"
                />
              </div>
            </div>
          </section>
        </>
      )}
    </div>
  );
}
