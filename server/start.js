import { createNomiServer } from "./app.js";

const port = Number(process.env.PORT || 5173);
const server = createNomiServer();
server.listen(port, process.env.HOST || "127.0.0.1", () =>
  console.log(`Nomi is listening on port ${port}`),
);
