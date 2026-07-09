/**
 * docs-quiz.js — selvstændig quiz på docs.html (loades separat så den ikke påvirkes af fejl i docs-demos.js).
 */
(function () {
  const quiz = [
    {
      q: "Hvorfor sender SPA'en code_challenge i stedet for code_verifier i authorize-kaldet?",
      options: [
        "Fordi challenge er kortere og sparer båndbredde",
        "Fordi authorize-URL'en kan ses i logs/historik — challengen kan ikke regnes tilbage til verifieren",
        "Fordi serveren ikke understøtter lange parametre",
      ],
      answer: 1,
      why: "URL'er lækker (browserhistorik, proxies, logs). SHA-256 er envejs, så en lækket challenge er ubrugelig uden verifieren, der først sendes i token-kaldet (POST, direkte).",
    },
    {
      q: "En kollega gemmer refresh tokens i localStorage “så brugeren ikke skal logge ind igen i morgen”. Hvad er problemet?",
      options: [
        "localStorage slettes ved reload, så det virker ikke",
        "localStorage overlever lukket browser OG kan læses af al JavaScript på siden — XSS giver angriberen et langlivet token",
        "Der er intet problem — refresh tokens er alligevel offentlige",
      ],
      answer: 1,
      why: "Refresh tokens lever i dage. XSS + localStorage = angriberen kan udstede nye access tokens længe efter. sessionStorage (eller memory) begrænser skaden til én fane/session.",
    },
    {
      q: "Dit API modtager et JWT hvor payloaden siger role=Admin. Hvornår må du stole på det?",
      options: [
        "Med det samme — payloaden er jo Base64-kodet",
        "Efter signaturen er verificeret mod JWKS og iss, aud og exp er tjekket",
        "Når access tokenet også findes i databasen",
      ],
      answer: 1,
      why: "Base64URL er kodning, ikke kryptering — alle kan skrive role=Admin i en payload. Kun signaturen (og iss/aud/exp) beviser at auth-serveren står bag. JWT'er slås netop IKKE op i databasen.",
    },
    {
      q: "Hvad er forskellen på OAuth 2.0 og OIDC?",
      options: [
        "OIDC er en nyere version af OAuth med bedre kryptering",
        "OAuth giver adgang (autorisation); OIDC lægger standardiseret identitet ovenpå (id_token, discovery, amr)",
        "OAuth er til API'er, OIDC er til databaser",
      ],
      answer: 1,
      why: "OAuth svarer på “må appen kalde API'et?”. OIDC svarer på “hvem er brugeren, og hvordan loggede de ind?” — via id_token med claims som amr.",
    },
    {
      q: "Brugeren trykker “Log ud” i din SPA, som kun rydder sessionStorage. Hvad sker der når de trykker “Log ind” igen?",
      options: [
        "De skal taste password igen som normalt",
        "De bliver logget ind automatisk uden login-side — session-cookien på auth-domænet lever stadig",
        "De får en fejl fordi det gamle token stadig findes",
      ],
      answer: 1,
      why: "Der er to “logget ind”: tokens i din app og session-cookien på auth-hosten. Rigtig logout kræver også GET /signout?returnUrl=… så cookien slettes.",
    },
    {
      q: "Hvorfor skal redirect_uri matche whitelisten PRÆCIST (og ikke bare “starte med” det rigtige domæne)?",
      options: [
        "Det er bare en gammel konvention",
        "Prefix-match kan omgås (fx https://din-app.dk.evil.com eller åbne redirects) så koden sendes til angriberen",
        "Fordi URL'er ikke kan sammenlignes på andre måder",
      ],
      answer: 1,
      why: "Authorization-koden leveres via redirect. Kan angriberen få den sendt til noget de kontrollerer (subdomæne-tricks, path traversal, open redirect), er flowet brudt. Eksakt streng-match lukker hele klassen af angreb.",
    },
  ];

  const wrap = document.getElementById("quiz-wrap");
  const scoreEl = document.getElementById("quiz-score");
  const restartBtn = document.getElementById("quiz-restart");
  if (!wrap || !scoreEl || !restartBtn) return;

  const letters = ["A", "B", "C", "D"];
  let answered = [];

  function updateScore() {
    const done = answered.filter((a) => a !== null).length;
    const score = answered.filter((a, i) => a === quiz[i].answer).length;
    scoreEl.classList.remove("quiz-score-perfect", "quiz-score-done");
    if (done === quiz.length) {
      scoreEl.textContent =
        "Færdig: " + score + " af " + quiz.length + " rigtige" +
        (score === quiz.length ? " — du er klar til at bygge integrationer!" : " — genlæs afsnittene og prøv igen.");
      scoreEl.classList.add(score === quiz.length ? "quiz-score-perfect" : "quiz-score-done");
    } else {
      scoreEl.textContent = done + " af " + quiz.length + " besvaret";
    }
  }

  function buildQuiz() {
    answered = new Array(quiz.length).fill(null);
    wrap.innerHTML = "";
    updateScore();

    quiz.forEach((item, qi) => {
      const box = document.createElement("div");
      box.className = "quiz-item";

      const label = document.createElement("span");
      label.className = "quiz-item-label";
      label.textContent = "Spørgsmål " + (qi + 1) + " af " + quiz.length;
      box.appendChild(label);

      const h = document.createElement("p");
      h.className = "quiz-q";
      h.textContent = item.q;
      box.appendChild(h);

      const feedback = document.createElement("div");
      feedback.className = "quiz-feedback";
      feedback.hidden = true;

      item.options.forEach((opt, oi) => {
        const btn = document.createElement("button");
        btn.type = "button";
        btn.className = "quiz-option";

        const letter = document.createElement("span");
        letter.className = "quiz-letter";
        letter.textContent = letters[oi] || "?";

        const text = document.createElement("span");
        text.className = "quiz-text";
        text.textContent = opt;

        btn.append(letter, text);
        btn.addEventListener("click", () => {
          if (answered[qi] !== null) return;
          answered[qi] = oi;
          const correct = oi === item.answer;
          const options = box.querySelectorAll(".quiz-option");

          btn.classList.add(correct ? "quiz-correct" : "quiz-wrong");
          letter.textContent = correct ? "\u2713" : "\u2717";
          if (!correct) {
            const right = options[item.answer];
            right.classList.add("quiz-correct");
            right.querySelector(".quiz-letter").textContent = "\u2713";
          }
          for (const b of options) b.disabled = true;

          feedback.hidden = false;
          feedback.classList.add(correct ? "quiz-feedback-ok" : "quiz-feedback-err");
          feedback.innerHTML =
            "<strong>" + (correct ? "Rigtigt!" : "Ikke helt.") + "</strong> " + item.why;
          updateScore();
        });
        box.appendChild(btn);
      });

      box.appendChild(feedback);
      wrap.appendChild(box);
    });
  }

  restartBtn.addEventListener("click", () => {
    buildQuiz();
    document.getElementById("quiz").scrollIntoView({ behavior: "smooth" });
  });

  buildQuiz();
})();
