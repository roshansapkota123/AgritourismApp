import React, { useState, useEffect } from "react";

import SERVER_ROOT from "../../baseUrl";

function Data() {
  var items = [];

  useEffect(() => {
    fetch(`${SERVER_ROOT}/api/farm-fields`)
      .then((res) => res.json())
      .then(
        (result) => {
          result.map((result) => {
            items.push({
              id: result.id,
              title: result.name,
              description: result.description,
              src: result.src,
            });
            console.log("as soon as populated", items);
            var api = `${SERVER_ROOT}/api/UnitPrices/` + result.id;
            console.log("result id is", result.id);
            //second fetch call
            fetch(api)
              .then((x) => x.json())
              .then(
                (Result2) => {
                  items[result.id - 1].sprice = Result2.sprice;
                  items[result.id - 1].mprice = Result2.mprice;
                  items[result.id - 1].lprice = Result2.lprice;
                },
                (error) => {
                  console.log("this is inner fetch", error);
                }
              ); //end of second fetch
          });
        },
        (error) => {
          console.log("this is outer fetch ", error);
        }
      );
  }); //useEffec function
  console.log("my items are", items);
  return items;
}
export default Data;
